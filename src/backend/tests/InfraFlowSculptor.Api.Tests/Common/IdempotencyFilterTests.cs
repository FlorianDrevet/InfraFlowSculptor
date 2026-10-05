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

    private static WebApplication CreateApp(WriteCounter counter, IIdempotencyStore store)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddScoped<IdempotencyEndpointFilter>();
        builder.Services.AddSingleton(counter);
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton<ICurrentOrganization>(
            new TestCurrentOrganization(new OrganizationId(Guid.NewGuid())));

        var app = builder.Build();
        app.MapPost(
                "/writes",
                (WriteRequest request, WriteCounter handlerCounter) =>
                {
                    var invocation = Interlocked.Increment(ref handlerCounter.Count);
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
    }

    private sealed class TestCurrentOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }

    private sealed class InMemoryIdempotencyStore : IIdempotencyStore
    {
        private readonly Dictionary<(OrganizationId OrganizationId, Guid Key), IdempotencyEntry> entries = [];

        public Task<IdempotencyEntry?> FindAsync(
            OrganizationId organizationId,
            Guid key,
            CancellationToken cancellationToken = default)
        {
            lock (entries)
            {
                entries.TryGetValue((organizationId, key), out var entry);
                return Task.FromResult(entry);
            }
        }

        public Task<bool> TryStartAsync(
            OrganizationId organizationId,
            Guid key,
            string requestHash,
            CancellationToken cancellationToken = default)
        {
            lock (entries)
            {
                var entry = new IdempotencyEntry(organizationId, key, requestHash, null);
                return Task.FromResult(entries.TryAdd((organizationId, key), entry));
            }
        }

        public Task CompleteAsync(
            OrganizationId organizationId,
            Guid key,
            IdempotencyResponse response,
            CancellationToken cancellationToken = default)
        {
            lock (entries)
            {
                var entry = entries[(organizationId, key)];
                entries[(organizationId, key)] = entry with { Response = response };
                return Task.CompletedTask;
            }
        }

        public Task DeleteAsync(
            OrganizationId organizationId,
            Guid key,
            CancellationToken cancellationToken = default)
        {
            lock (entries)
            {
                entries.Remove((organizationId, key));
                return Task.CompletedTask;
            }
        }

    }
}
