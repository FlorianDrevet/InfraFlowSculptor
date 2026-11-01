using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Naming.Availability;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Infrastructure.Persistence.Naming;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class NameAvailabilityStoreTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task DeduplicatesPendingRequestsAndRequeuesExpiredCacheRows()
    {
        var now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = postgres.CreatePersistenceDbContext(new FixedOrganization());
        await dbContext.Database.MigrateAsync();
        var store = new EfNameAvailabilityStore(dbContext);

        await store.EnqueueIfMissingOrExpiredAsync("VAULT.AZURE.NET", ["IFS-SHOP-DEV"], now);
        await store.EnqueueIfMissingOrExpiredAsync("vault.azure.net", ["ifs-shop-dev"], now.AddSeconds(1));

        var pending = await store.FindPendingAsync(now.AddSeconds(1), 10);
        Assert.Equal("ifs-shop-dev", Assert.Single(pending).Name);

        await store.RecordAttemptAsync(
            "vault.azure.net",
            "ifs-shop-dev",
            new NameAvailabilityUpdate(
                NameAvailabilityState.Resolved,
                now.AddHours(24),
                now,
                now.AddHours(24),
                0,
                null));

        var fresh = await store.FindManyAsync("vault.azure.net", ["ifs-shop-dev"]);
        Assert.Equal(NameAvailabilityState.Resolved, fresh["ifs-shop-dev"].State);

        await store.EnqueueIfMissingOrExpiredAsync("vault.azure.net", ["ifs-shop-dev"], now.AddHours(24));

        var requeued = await store.FindPendingAsync(now.AddHours(24), 10);
        Assert.Equal("ifs-shop-dev", Assert.Single(requeued).Name);
        Assert.Equal(1, await dbContext.Set<NameAvailabilityEntity>().CountAsync());

        var expired = new NameAvailabilityEntity("vault.azure.net", "ifs-shop-expired", now.AddDays(-2));
        expired.Apply(new NameAvailabilityUpdate(
            NameAvailabilityState.Resolved,
            now.AddDays(-1),
            now.AddDays(-2),
            now.AddDays(-1),
            0,
            null));
        dbContext.Add(expired);
        await dbContext.SaveChangesAsync();

        await store.DeleteExpiredAsync(now);

        Assert.Equal(1, await dbContext.Set<NameAvailabilityEntity>().CountAsync());
    }

    private sealed class FixedOrganization : ICurrentOrganization
    {
        public OrganizationId? Id => null;
    }
}
