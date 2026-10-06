using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Infrastructure.Tests.Persistence;
using InfraFlowSculptor.Infrastructure.Persistence.Admin;

namespace InfraFlowSculptor.Infrastructure.Tests.Jobs;

public sealed class ScheduledJobLeaseTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task FailedJobCanReleaseItsLeaseForRetry()
    {
        var organization = new OrganizationId(Guid.NewGuid());
        await using var firstContext = postgres.CreateDbContext(new TestOrganization(organization));
        await using var secondContext = postgres.CreateDbContext(new TestOrganization(organization));
        await firstContext.Database.EnsureCreatedAsync();
        var firstRunner = new EfScheduledJobLeaseStore(firstContext);
        var secondRunner = new EfScheduledJobLeaseStore(secondContext);
        var now = DateTimeOffset.UtcNow;

        Assert.True(await firstRunner.TryAcquireAsync("retry-lease-test", "runner-a", now, now.AddHours(1)));

        await firstRunner.ReleaseAsync("retry-lease-test", "runner-a", now);

        Assert.True(await secondRunner.TryAcquireAsync(
            "retry-lease-test",
            "runner-b",
            now,
            now.AddMinutes(1)));
    }

    [Fact]
    public async Task ConcurrentRunnersCanAcquireOnlyOneLease()
    {
        var organization = new OrganizationId(Guid.NewGuid());
        await using var firstContext = postgres.CreateDbContext(new TestOrganization(organization));
        await using var secondContext = postgres.CreateDbContext(new TestOrganization(organization));
        await firstContext.Database.EnsureCreatedAsync();
        var firstRunner = new EfScheduledJobLeaseStore(firstContext);
        var secondRunner = new EfScheduledJobLeaseStore(secondContext);
        var now = DateTimeOffset.UtcNow;

        var acquisitions = await Task.WhenAll(
            firstRunner.TryAcquireAsync("lease-test", "runner-a", now, now.AddMinutes(1)),
            secondRunner.TryAcquireAsync("lease-test", "runner-b", now, now.AddMinutes(1)));

        Assert.Single(acquisitions, acquired => acquired);
    }

    [Fact]
    public async Task RenewedLeasePreventsAnotherRunnerFromStartingLongJob()
    {
        var organization = new OrganizationId(Guid.NewGuid());
        await using var firstContext = postgres.CreateDbContext(new TestOrganization(organization));
        await using var secondContext = postgres.CreateDbContext(new TestOrganization(organization));
        await firstContext.Database.EnsureCreatedAsync();
        var firstRunner = new EfScheduledJobLeaseStore(firstContext);
        var secondRunner = new EfScheduledJobLeaseStore(secondContext);
        var startedAt = DateTimeOffset.UtcNow;

        Assert.True(await firstRunner.TryAcquireAsync(
            "renewed-lease-test",
            "runner-a",
            startedAt,
            startedAt.AddMinutes(1)));
        Assert.True(await firstRunner.RenewAsync(
            "renewed-lease-test",
            "runner-a",
            startedAt.AddMinutes(3)));

        Assert.False(await secondRunner.TryAcquireAsync(
            "renewed-lease-test",
            "runner-b",
            startedAt.AddMinutes(2),
            startedAt.AddMinutes(3)));
        Assert.True(await secondRunner.TryAcquireAsync(
            "renewed-lease-test",
            "runner-b",
            startedAt.AddMinutes(4),
            startedAt.AddMinutes(5)));
    }

    private sealed class TestOrganization(OrganizationId id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }
}
