using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class OrganizationFilterTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task QueryOnlyReturnsRowsForTheCurrentOrganization()
    {
        var organizationA = new OrganizationId(Guid.NewGuid());
        var organizationB = new OrganizationId(Guid.NewGuid());

        await using (var seed = postgres.CreateDbContext(new TestCurrentOrganization(organizationA)))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.OrganizationOwnedEntities.AddRange(
                new OrganizationOwnedTestEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationA,
                    Name = "visible"
                },
                new OrganizationOwnedTestEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationB,
                    Name = "hidden"
                });
            await seed.SaveChangesAsync();
        }

        await using var context = postgres.CreateDbContext(new TestCurrentOrganization(organizationA));
        var rows = await context.OrganizationOwnedEntities.ToListAsync();

        Assert.Single(rows);
        Assert.Equal("visible", rows[0].Name);
    }

    private sealed class TestCurrentOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }
}
