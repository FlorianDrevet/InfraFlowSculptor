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
        var scope = IdempotencyScope.ForOrganization(organizationId, new UserKey(Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(await store.TryStartAsync(scope, key, requestHash));
        Assert.False(await store.TryStartAsync(scope, key, requestHash));

        var response = new IdempotencyResponse(
            StatusCode: 201,
            ContentType: "application/json",
            Headers: new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            Body: "{\"id\":\"created\"}"u8.ToArray());
        await store.CompleteAsync(scope, key, response);

        var entry = await store.FindAsync(scope, key);
        Assert.NotNull(entry);
        Assert.Equal(requestHash, entry.RequestHash);
        Assert.Equal(response.StatusCode, entry.Response?.StatusCode);
        Assert.Equal(response.Body, entry.Response?.Body);
    }

    [Fact]
    public async Task OrganizationKeysAreIsolatedAcrossUsers()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var firstUserScope = IdempotencyScope.ForOrganization(
            organizationId,
            new UserKey(Guid.NewGuid(), Guid.NewGuid()));
        var secondUserScope = IdempotencyScope.ForOrganization(
            organizationId,
            new UserKey(Guid.NewGuid(), Guid.NewGuid()));
        var key = Guid.NewGuid();
        const string requestHash = "c8d8c8dc8b6f1cbb154c4387de8d6d5e95ed2417f5c2dd7e5f0bd6d7bdc4f2d9";
        await using var context = postgres.CreateDbContext(new TestCurrentOrganization(organizationId));
        await context.Database.EnsureCreatedAsync();
        var store = new EfIdempotencyStore(context, TimeProvider.System);

        Assert.True(await store.TryStartAsync(firstUserScope, key, requestHash));
        Assert.True(await store.TryStartAsync(secondUserScope, key, requestHash));
        Assert.False(await store.TryStartAsync(firstUserScope, key, requestHash));

        var firstResponse = new IdempotencyResponse(
            StatusCode: 201,
            ContentType: "application/json",
            Headers: new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            Body: "{\"owner\":\"first\"}"u8.ToArray());
        await store.CompleteAsync(firstUserScope, key, firstResponse);

        var secondEntry = await store.FindAsync(secondUserScope, key);
        Assert.NotNull(secondEntry);
        Assert.Null(secondEntry.Response);
    }

    [Fact]
    public async Task UserScopedKeyIsReservedOnceAndPersistsItsResponse()
    {
        var userKey = new UserKey(Guid.NewGuid(), Guid.NewGuid());
        var scope = IdempotencyScope.ForUser(userKey);
        var key = Guid.NewGuid();
        const string requestHash = "d8d8c8dc8b6f1cbb154c4387de8d6d5e95ed2417f5c2dd7e5f0bd6d7bdc4f2d9";
        await using var context = postgres.CreateDbContext(new TestCurrentOrganization(null));
        await context.Database.EnsureCreatedAsync();
        var store = new EfIdempotencyStore(context, TimeProvider.System);

        Assert.True(await store.TryStartAsync(scope, key, requestHash));
        Assert.False(await store.TryStartAsync(scope, key, requestHash));

        var response = new IdempotencyResponse(
            StatusCode: 201,
            ContentType: "application/json",
            Headers: new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            Body: "{\"id\":\"created\"}"u8.ToArray());
        await store.CompleteAsync(scope, key, response);

        var entry = await store.FindAsync(scope, key);
        Assert.NotNull(entry);
        Assert.Equal(requestHash, entry.RequestHash);
        Assert.Equal(response.StatusCode, entry.Response?.StatusCode);
        Assert.Equal(response.Body, entry.Response?.Body);
    }

    [Fact]
    public async Task ReservationAndResponseAreVisibleOnlyAfterTheOwningTransactionCommits()
    {
        var scope = IdempotencyScope.ForUser(new UserKey(Guid.NewGuid(), Guid.NewGuid()));
        var key = Guid.NewGuid();
        const string requestHash = "b8d8c8dc8b6f1cbb154c4387de8d6d5e95ed2417f5c2dd7e5f0bd6d7bdc4f2d9";
        var response = new IdempotencyResponse(
            StatusCode: 201,
            ContentType: "application/json",
            Headers: new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            Body: "{\"id\":\"committed\"}"u8.ToArray());

        await using var context = postgres.CreateDbContext(new TestCurrentOrganization(null));
        await context.Database.EnsureCreatedAsync();
        var store = new EfIdempotencyStore(context, TimeProvider.System);
        await using var transaction = await store.BeginTransactionAsync();

        Assert.True(await store.TryStartAsync(scope, key, requestHash));
        await store.CompleteAsync(scope, key, response);
        await using (var observer = postgres.CreateDbContext(new TestCurrentOrganization(null)))
        {
            var observerStore = new EfIdempotencyStore(observer, TimeProvider.System);
            Assert.Null(await observerStore.FindAsync(scope, key));
        }

        await transaction.CommitAsync();

        await using var committedObserver = postgres.CreateDbContext(new TestCurrentOrganization(null));
        var committedStore = new EfIdempotencyStore(committedObserver, TimeProvider.System);
        var entry = await committedStore.FindAsync(scope, key);
        Assert.NotNull(entry);
        Assert.Equal(response.Body, entry.Response?.Body);
    }

    [Fact]
    public async Task ReservationDisappearsWhenTheOwningTransactionRollsBack()
    {
        var scope = IdempotencyScope.ForUser(new UserKey(Guid.NewGuid(), Guid.NewGuid()));
        var key = Guid.NewGuid();
        const string requestHash = "a8d8c8dc8b6f1cbb154c4387de8d6d5e95ed2417f5c2dd7e5f0bd6d7bdc4f2d9";

        await using var context = postgres.CreateDbContext(new TestCurrentOrganization(null));
        await context.Database.EnsureCreatedAsync();
        var store = new EfIdempotencyStore(context, TimeProvider.System);
        await using (var transaction = await store.BeginTransactionAsync())
        {
            Assert.True(await store.TryStartAsync(scope, key, requestHash));
            await transaction.RollbackAsync();
        }

        await using var observer = postgres.CreateDbContext(new TestCurrentOrganization(null));
        var observerStore = new EfIdempotencyStore(observer, TimeProvider.System);
        Assert.Null(await observerStore.FindAsync(scope, key));
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
        var userKey = new UserKey(Guid.NewGuid(), Guid.NewGuid());
        var userEntry = new UserIdempotencyKeyEntity(
            userKey,
            Guid.NewGuid(),
            new string('b', 64),
            DateTimeOffset.UtcNow.AddHours(-25));

        await using var context = postgres.CreateDbContext(new TestCurrentOrganization(organizationId));
        await context.Database.EnsureCreatedAsync();
        context.IdempotencyKeys.Add(key);
        context.UserIdempotencyKeys.Add(userEntry);
        await context.SaveChangesAsync();

        var purger = new IdempotencyKeyPurge(context);
        var deleted = await purger.DeleteExpiredAsync(DateTimeOffset.UtcNow.AddHours(-24));

        Assert.Equal(2, deleted);
        Assert.False(await context.IdempotencyKeys
            .IgnoreQueryFilters()
            .AnyAsync(candidate => candidate.Key == key.Key));
        Assert.False(await context.UserIdempotencyKeys
            .AnyAsync(candidate => candidate.Key == userEntry.Key));
    }

    private sealed class TestCurrentOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }
}
