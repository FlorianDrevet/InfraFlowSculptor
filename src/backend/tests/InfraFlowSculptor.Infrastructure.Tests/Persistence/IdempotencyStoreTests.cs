using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Infrastructure.Persistence.Admin;
using InfraFlowSculptor.Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class IdempotencyStoreTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task KeyIsReservedOnceAndPersistsItsResponse()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var key = Guid.NewGuid();
        const string requestHash = "c8d8c8dc8b6f1cbb154c4387de8d6d5e95ed2417f5c2dd7e5f0bd6d7bdc4f2d9";
        await using var context = postgres.CreateDbContext(new TestCurrentOrganization(organizationId));
        await context.Database.EnsureCreatedAsync();
        var store = new EfIdempotencyStore(context, TimeProvider.System);

        Assert.True(await store.TryStartAsync(organizationId, key, requestHash));
        Assert.False(await store.TryStartAsync(organizationId, key, requestHash));

        var response = new IdempotencyResponse(
            StatusCode: 201,
            ContentType: "application/json",
            Headers: new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            Body: "{\"id\":\"created\"}"u8.ToArray());
        await store.CompleteAsync(organizationId, key, response);

        var entry = await store.FindAsync(organizationId, key);
        Assert.NotNull(entry);
        Assert.Equal(requestHash, entry.RequestHash);
        Assert.Equal(response.StatusCode, entry.Response?.StatusCode);
        Assert.Equal(response.Body, entry.Response?.Body);
    }

    [Fact]
    public async Task PurgeRemovesKeysOlderThanTheRetentionCutoff()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var key = new IdempotencyKeyEntity
        {
            OrganizationId = organizationId,
            Key = Guid.NewGuid(),
            RequestHash = new string('a', 64),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-25)
        };

        await using var context = postgres.CreateDbContext(new TestCurrentOrganization(organizationId));
        await context.Database.EnsureCreatedAsync();
        context.IdempotencyKeys.Add(key);
        await context.SaveChangesAsync();

        var purger = new IdempotencyKeyPurge(context);
        var deleted = await purger.DeleteExpiredAsync(DateTimeOffset.UtcNow.AddHours(-24));

        Assert.Equal(1, deleted);
        Assert.False(await context.IdempotencyKeys
            .IgnoreQueryFilters()
            .AnyAsync(candidate => candidate.Key == key.Key));
    }

    private sealed class TestCurrentOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }
}
