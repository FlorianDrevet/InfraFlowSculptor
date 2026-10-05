using System.Security.Cryptography;
using System.Text.Json;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace InfraFlowSculptor.Api.Common.Idempotency;

public sealed class IdempotencyEndpointFilter(
    IIdempotencyStore store,
    ICurrentOrganization currentOrganization)
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

        if (currentOrganization.Id is not { } organizationId)
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "ORGANIZATION_CONTEXT_REQUIRED",
                "An active organization is required for an idempotent write.");
        }

        var requestHash = ComputeRequestHash(context, requestArgumentIndexes);
        var cancellationToken = context.HttpContext.RequestAborted;
        var entry = await store.FindAsync(organizationId, key, cancellationToken);
        if (entry is not null)
        {
            var existingResult = ExistingEntryResult(entry, requestHash);
            if (existingResult is not null)
            {
                return existingResult;
            }
        }
        else if (await store.TryStartAsync(organizationId, key, requestHash, cancellationToken))
        {
            return await WrapResponseAsync(context, next, organizationId, key);
        }

        for (var attempt = 0; attempt < PendingRequestAttempts; attempt++)
        {
            await Task.Delay(PendingRequestDelay, cancellationToken);
            entry = await store.FindAsync(organizationId, key, cancellationToken);
            if (entry is not null)
            {
                var existingResult = ExistingEntryResult(entry, requestHash);
                if (existingResult is not null)
                {
                    return existingResult;
                }
            }
            else if (await store.TryStartAsync(organizationId, key, requestHash, cancellationToken))
            {
                return await WrapResponseAsync(context, next, organizationId, key);
            }
        }

        context.HttpContext.Response.Headers["Retry-After"] = "1";
        return Problem(
            StatusCodes.Status409Conflict,
            "IDEMPOTENCY_IN_PROGRESS",
            "A request with this idempotency key is still being processed.");
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
        OrganizationId organizationId,
        Guid key)
    {
        var result = await next(context);
        if (result is not IResult httpResult)
        {
            await store.DeleteAsync(organizationId, key, context.HttpContext.RequestAborted);
            throw new InvalidOperationException("Endpoints using WithIdempotency must return an IResult.");
        }

        return new CaptureResponseResult(store, organizationId, key, httpResult);
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
        OrganizationId organizationId,
        Guid key,
        IResult result) : IResult
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
                await store.CompleteAsync(organizationId, key, response, context.RequestAborted);

                context.Features.Set(originalFeature);
                bodyBuffer.Position = 0;
                await bodyBuffer.CopyToAsync(originalFeature.Stream, context.RequestAborted);
            }
            catch
            {
                context.Features.Set(originalFeature);
                await store.DeleteAsync(organizationId, key, CancellationToken.None);
                throw;
            }
            finally
            {
                context.Features.Set(originalFeature);
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
