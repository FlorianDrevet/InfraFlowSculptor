using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Infrastructure.Authentication;
using InfraFlowSculptor.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Tests.Authentication;

public sealed class EfProjectOwnershipCheckTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task RemovingMemberIsBlockedOnlyWhenTheyAreTheProjectsLastOwner()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var aliceId = new UserProfileId(Guid.NewGuid());
        var bobId = new UserProfileId(Guid.NewGuid());
        var chloeId = new UserProfileId(Guid.NewGuid());
        var organization = OrganizationAggregate.Create(organizationId, "Project ownership", aliceId).Value;

        await using var context = postgres.CreatePersistenceDbContext(new TestCurrentOrganization(organizationId));
        await context.Database.EnsureCreatedAsync();
        context.Organizations.Add(organization);

        var project = ProjectAggregate.Create(
            new ProjectId(Guid.NewGuid()), organizationId, "Shop One", "shop1", aliceId).Value;
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        var ownershipCheck = new EfProjectOwnershipCheck(context);
        Assert.True(await ownershipCheck.WouldRemovingMemberRemoveLastProjectOwnerAsync(
            organizationId, aliceId, CancellationToken.None));
        Assert.False(await ownershipCheck.WouldRemovingMemberRemoveLastProjectOwnerAsync(
            organizationId, bobId, CancellationToken.None));

        Assert.False(project.AssignRole(bobId, ProjectRole.Owner).IsError);
        await context.SaveChangesAsync();
        Assert.False(await ownershipCheck.WouldRemovingMemberRemoveLastProjectOwnerAsync(
            organizationId, aliceId, CancellationToken.None));

        var secondProject = ProjectAggregate.Create(
            new ProjectId(Guid.NewGuid()), organizationId, "Shop Two", "shop2", chloeId).Value;
        context.Projects.Add(secondProject);
        await context.SaveChangesAsync();
        Assert.True(await ownershipCheck.WouldRemovingMemberRemoveLastProjectOwnerAsync(
            organizationId, chloeId, CancellationToken.None));
    }

    private sealed class TestCurrentOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }
}
