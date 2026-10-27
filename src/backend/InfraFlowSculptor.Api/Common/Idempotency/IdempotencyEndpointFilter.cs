using System.Security.Cryptography;
using System.Text.Json;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace InfraFlowSculptor.Api.Common.Idempotency;

public sealed class IdempotencyEndpointFilter(
    IIdempotencyStore store,
    ICurrentOrganization currentOrganization,
    ICurrentUser currentUser)
{
    public const string HeaderName = "Idempotency-Key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan PendingRequestDelay = TimeSpan.FromMilliseconds(50);
    private const int PendingRequestAttempts = 600;

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        IReadOnlyCollection<int> requestArgumentIndexes)
    {
        var request = context.HttpContext.Request;
        if (!request.Headers.TryGetValue(HeaderName, out StringValues headerValues)
            || !Guid.TryParse(headerValues.FirstOrDefault(), out var key))
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "IDEMPOTENCY_KEY_REQUIRED",
                "A valid UUID must be supplied in the Idempotency-Key header.");
        }

        var scope = currentUser.IsAuthenticated && !currentUser.Key.IsEmpty
            ? currentOrganization.Id is { } organizationId
                ? IdempotencyScope.ForOrganization(organizationId, currentUser.Key)
                : IdempotencyScope.ForUser(currentUser.Key)
            : null;
        if (scope is null)
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "IDEMPOTENCY_SCOPE_REQUIRED",
                "An authenticated user or active organization is required for an idempotent write.");
        }

        var requestHash = ComputeRequestHash(context, requestArgumentIndexes);
        var cancellationToken = context.HttpContext.RequestAborted;
        var entry = await store.FindAsync(scope, key, cancellationToken);
        if (entry is not null)
        {
            var existingResult = ExistingEntryResult(entry, requestHash);
            if (existingResult is not null)
            {
                return existingResult;
            }
        }
        else if (await TryStartAsync(context, next, scope, key, requestHash, cancellationToken) is { } result)
        {
            return result;
        }

        for (var attempt = 0; attempt < PendingRequestAttempts; attempt++)
        {
            await Task.Delay(PendingRequestDelay, cancellationToken);
            entry = await store.FindAsync(scope, key, cancellationToken);
            if (entry is not null)
            {
                var existingResult = ExistingEntryResult(entry, requestHash);
                if (existingResult is not null)
                {
                    return existingResult;
                }
            }
            else if (await TryStartAsync(context, next, scope, key, requestHash, cancellationToken) is { } result)
            {
                return result;
            }
        }

        context.HttpContext.Response.Headers["Retry-After"] = "1";
        return Problem(
            StatusCodes.Status409Conflict,
            "IDEMPOTENCY_IN_PROGRESS",
            "A request with this idempotency key is still being processed.");
    }

    private async ValueTask<object?> TryStartAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        IdempotencyScope scope,
        Guid key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var transaction = await store.BeginTransactionAsync(cancellationToken);
        try
        {
            if (!await store.TryStartAsync(scope, key, requestHash, cancellationToken))
            {
                await RollbackAndDisposeAsync(transaction);
                return null;
            }

            return await WrapResponseAsync(context, next, scope, key, transaction);
        }
        catch
        {
            await RollbackAndDisposeAsync(transaction);
            throw;
        }
    }

    private static object? ExistingEntryResult(IdempotencyEntry entry, string requestHash)
    {
        if (!string.Equals(entry.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return Problem(
                StatusCodes.Status422UnprocessableEntity,
                "IDEMPOTENCY_KEY_REUSED",
                "This idempotency key was already used with a different request.");
        }

        return entry.Response is null ? null : new ReplayResponseResult(entry.Response);
    }

    private async ValueTask<object?> WrapResponseAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        IdempotencyScope scope,
        Guid key,
        IIfsDbTransaction transaction)
    {
        var result = await next(context);

        if (result is not IResult httpResult)
        {
            throw new InvalidOperationException("Endpoints using WithIdempotency must return an IResult.");
        }

        return new CaptureResponseResult(store, scope, key, httpResult, transaction);
    }

    private static async Task RollbackAndDisposeAsync(IIfsDbTransaction transaction)
    {
        try
        {
            if (transaction.IsOwner)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    private static string ComputeRequestHash(
        EndpointFilterInvocationContext context,
        IReadOnlyCollection<int> requestArgumentIndexes)
    {
        var arguments = requestArgumentIndexes
            .Where(index => index < context.Arguments.Count)
            .Select(index => new
            {
                Type = context.Arguments[index]?.GetType().FullName,
                Value = context.Arguments[index]
            })
            .ToArray();
        var request = context.HttpContext.Request;
        var canonicalRequest = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                request.Method,
                Path = request.Path.Value,
                Query = request.QueryString.Value,
                Arguments = arguments
            },
            JsonOptions);

        return Convert.ToHexString(SHA256.HashData(canonicalRequest));
    }

    private static IResult Problem(int statusCode, string code, string detail) =>
        Results.Problem(
            statusCode: statusCode,
            title: "The idempotency request could not be processed.",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private sealed class CaptureResponseResult(
        IIdempotencyStore store,
        IdempotencyScope scope,
        Guid key,
        IResult result,
        IIfsDbTransaction transaction) : IResult
    {
        public async Task ExecuteAsync(HttpContext context)
        {
            var originalFeature = context.Features.Get<IHttpResponseBodyFeature>()
                ?? throw new InvalidOperationException("The HTTP response body feature is unavailable.");
            await using var bodyBuffer = new MemoryStream();
            context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(bodyBuffer));

            try
            {
                await result.ExecuteAsync(context);
                await context.Features.Get<IHttpResponseBodyFeature>()!.CompleteAsync();

                var response = new IdempotencyResponse(
                    context.Response.StatusCode,
                    context.Response.ContentType,
                    CaptureHeaders(context.Response.Headers),
                    bodyBuffer.ToArray());
                await store.CompleteAsync(scope, key, response, context.RequestAborted);
                await transaction.CommitAsync(context.RequestAborted);

                context.Features.Set(originalFeature);
                bodyBuffer.Position = 0;
                await bodyBuffer.CopyToAsync(originalFeature.Stream, context.RequestAborted);
            }
            catch
            {
                context.Features.Set(originalFeature);
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // The commit may have completed before the connection failed.
                }

                throw;
            }
            finally
            {
                context.Features.Set(originalFeature);
                await transaction.DisposeAsync();
            }
        }

        private static Dictionary<string, string[]> CaptureHeaders(IHeaderDictionary headers) =>
            headers
                .Where(header => !header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                    && !header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                    && !header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    header => header.Key,
                    header => header.Value.Where(value => value is not null).Select(value => value!).ToArray(),
                    StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ReplayResponseResult(IdempotencyResponse response) : IResult
    {
        public async Task ExecuteAsync(HttpContext context)
        {
            context.Response.StatusCode = response.StatusCode;
            context.Response.ContentType = response.ContentType;
            foreach (var (name, values) in response.Headers)
            {
                context.Response.Headers[name] = values;
            }

            if (response.Body.Length > 0)
            {
                await context.Response.Body.WriteAsync(response.Body, context.RequestAborted);
            }
        }
    }
}
