using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Api.Tests.Common;

public sealed class IdempotencyFilterTests
{
    [Fact]
    public async Task SameKeyAndBodyReplaysTheFirstResponseAndRunsHandlerOnce()
    {
        var counter = new WriteCounter();
        await using var app = CreateApp(counter, new InMemoryIdempotencyStore());
        await app.StartAsync();
        using var client = app.GetTestClient();
        var key = Guid.NewGuid().ToString("D");

        using var firstRequest = CreateRequest(key, "alpha");
        using var firstResponse = await client.SendAsync(firstRequest);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();

        using var retryRequest = CreateRequest(key, "alpha");
        using var retryResponse = await client.SendAsync(retryRequest);
        var retryBody = await retryResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.Equal(firstBody, retryBody);
        Assert.Equal(1, counter.Count);
    }

    [Fact]
    public async Task SameKeyWithDifferentBodyReturns422WithoutRunningHandlerAgain()
    {
        var counter = new WriteCounter();
        await using var app = CreateApp(counter, new InMemoryIdempotencyStore());
        await app.StartAsync();
        using var client = app.GetTestClient();
        var key = Guid.NewGuid().ToString("D");

        using var firstResponse = await client.SendAsync(CreateRequest(key, "alpha"));
        using var conflictingResponse = await client.SendAsync(CreateRequest(key, "beta"));
        var problem = await conflictingResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, conflictingResponse.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", problem.GetProperty("code").GetString());
        Assert.Equal(1, counter.Count);
    }

    [Fact]
    public async Task SameOrganizationKeyIsIsolatedAcrossUsers()
    {
        var counter = new WriteCounter();
        var currentUser = new TestCurrentUser();
        await using var app = CreateApp(counter, new InMemoryIdempotencyStore(), currentUser);
        await app.StartAsync();
        using var client = app.GetTestClient();
        var key = Guid.NewGuid().ToString("D");

        using var firstResponse = await client.SendAsync(CreateRequest(key, "alpha"));
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        currentUser.Key = new UserKey(Guid.NewGuid(), Guid.NewGuid());
        using var secondResponse = await client.SendAsync(CreateRequest(key, "alpha"));
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotEqual(firstBody, secondBody);
        Assert.Equal(2, counter.Count);
    }

    [Fact]
    public async Task HandlerExceptionReleasesTheIdempotencyKey()
    {
        var counter = new WriteCounter { ThrowOnNext = true };
        var store = new InMemoryIdempotencyStore();
        await using var app = CreateApp(counter, store);
        await app.StartAsync();
        using var client = app.GetTestClient();
        var key = Guid.NewGuid().ToString("D");

        using var failedResponse = await client.SendAsync(CreateRequest(key, "alpha"));

        Assert.Equal(HttpStatusCode.InternalServerError, failedResponse.StatusCode);
        Assert.Equal(0, store.Count);

        using var retryResponse = await client.SendAsync(CreateRequest(key, "alpha"));

        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.Equal(2, counter.Count);
    }

    [Fact]
    public async Task ResponsePersistenceFailureRollsBackTheReservationAndWriteTransaction()
    {
        var counter = new WriteCounter();
        var store = new InMemoryIdempotencyStore { ThrowOnComplete = true };
        await using var app = CreateApp(counter, store);
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest(Guid.NewGuid().ToString("D"), "alpha"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, counter.Count);
        Assert.Equal(0, store.Count);
    }

    private static WebApplication CreateApp(
        WriteCounter counter,
        IIdempotencyStore store,
        TestCurrentUser? currentUser = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddScoped<IdempotencyEndpointFilter>();
        builder.Services.AddSingleton(counter);
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton<ICurrentOrganization>(
            new TestCurrentOrganization(new OrganizationId(Guid.NewGuid())));
        builder.Services.AddSingleton<ICurrentUser>(currentUser ?? new TestCurrentUser());

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        });
        app.MapPost(
                "/writes",
                (WriteRequest request, WriteCounter handlerCounter) =>
                {
                    var invocation = Interlocked.Increment(ref handlerCounter.Count);
                    if (handlerCounter.ThrowOnNext)
                    {
                        handlerCounter.ThrowOnNext = false;
                        throw new InvalidOperationException("Test handler failure.");
                    }

                    return Results.Json(new { request.Value, invocation });
                })
            .WithIdempotency();

        return app;
    }

    private static HttpRequestMessage CreateRequest(string key, string value)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/writes")
        {
            Content = JsonContent.Create(new WriteRequest(value))
        };
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private sealed record WriteRequest(string Value);

    private sealed class WriteCounter
    {
        public int Count;

        public bool ThrowOnNext;
    }

    private sealed class TestCurrentOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public UserKey Key { get; set; } = new(Guid.NewGuid(), Guid.NewGuid());

        public string DisplayName => "Test User";

        public VerifiedEmail? VerifiedEmail => null;

        public bool IsAuthenticated => true;

        public AuthenticationKind AuthenticationKind => AuthenticationKind.Oidc;
    }

    private sealed class InMemoryIdempotencyStore : IIdempotencyStore
    {
        private readonly Dictionary<(IdempotencyScope Scope, Guid Key), IdempotencyEntry> entries = [];
        private (IdempotencyScope Scope, Guid Key)? transactionReservation;

        public bool ThrowOnComplete { get; init; }

        public Task<IIfsDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IIfsDbTransaction>(new InMemoryTransaction(this));

        public int Count
        {
            get
            {
                lock (entries)
                {
                    return entries.Count;
                }
            }
        }

        public Task<IdempotencyEntry?> FindAsync(
            IdempotencyScope scope,
            Guid key,
            CancellationToken cancellationToken = default)
        {
            lock (entries)
            {
                entries.TryGetValue((scope, key), out var entry);
                return Task.FromResult(entry);
            }
        }

        public Task<bool> TryStartAsync(
            IdempotencyScope scope,
            Guid key,
            string requestHash,
            CancellationToken cancellationToken = default)
        {
            lock (entries)
            {
                var entry = new IdempotencyEntry(requestHash, null);
                var added = entries.TryAdd((scope, key), entry);
                if (added)
                {
                    transactionReservation = (scope, key);
                }

                return Task.FromResult(added);
            }
        }

        public Task CompleteAsync(
            IdempotencyScope scope,
            Guid key,
            IdempotencyResponse response,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnComplete)
            {
                throw new InvalidOperationException("Test response persistence failure.");
            }

            lock (entries)
            {
                var entry = entries[(scope, key)];
                entries[(scope, key)] = entry with { Response = response };
                return Task.CompletedTask;
            }
        }

        public Task DeleteAsync(
            IdempotencyScope scope,
            Guid key,
            CancellationToken cancellationToken = default)
        {
            lock (entries)
            {
                entries.Remove((scope, key));
                return Task.CompletedTask;
            }
        }

        private void RollbackTransaction()
        {
            lock (entries)
            {
                if (transactionReservation is { } reservation)
                {
                    entries.Remove(reservation);
                    transactionReservation = null;
                }
            }
        }

        private void CommitTransaction()
        {
            lock (entries)
            {
                transactionReservation = null;
            }
        }

        private sealed class InMemoryTransaction(InMemoryIdempotencyStore store) : IIfsDbTransaction
        {
            private bool completed;

            public bool IsOwner => true;

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                if (!completed)
                {
                    store.CommitTransaction();
                    completed = true;
                }

                return Task.CompletedTask;
            }

            public Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                if (!completed)
                {
                    store.RollbackTransaction();
                    completed = true;
                }

                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                if (!completed)
                {
                    store.RollbackTransaction();
                    completed = true;
                }

                return ValueTask.CompletedTask;
            }
        }

    }
}
