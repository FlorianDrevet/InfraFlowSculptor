using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class ConcurrencyTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task SecondContextCannotOverwriteAConcurrentUpdate()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(organizationId);
        var entityId = Guid.NewGuid();

        await using (var seed = postgres.CreateDbContext(currentOrganization))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.OrganizationOwnedEntities.Add(new OrganizationOwnedTestEntity
            {
                Id = entityId,
                OrganizationId = organizationId,
                Name = "original"
            });
            await seed.SaveChangesAsync();
        }

        await using var firstContext = postgres.CreateDbContext(currentOrganization);
        await using var secondContext = postgres.CreateDbContext(currentOrganization);
        var first = await firstContext.OrganizationOwnedEntities.SingleAsync(entity => entity.Id == entityId);
        var second = await secondContext.OrganizationOwnedEntities.SingleAsync(entity => entity.Id == entityId);

        first.Name = "first";
        second.Name = "second";
        await firstContext.SaveChangesAsync();

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => secondContext.SaveChangesAsync());
    }

    private sealed class TestCurrentOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }
}
