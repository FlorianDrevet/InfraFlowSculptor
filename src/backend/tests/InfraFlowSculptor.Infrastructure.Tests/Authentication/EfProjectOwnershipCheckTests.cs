using InfraFlowSculptor.Application.Common.Persistence;
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

    [Fact]
    public async Task ConcurrentMemberAndProjectRoleChangesCannotRemoveEveryProjectOwner()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var aliceId = new UserProfileId(Guid.NewGuid());
        var bobId = new UserProfileId(Guid.NewGuid());
        var projectId = new ProjectId(Guid.NewGuid());
        var organization = OrganizationAggregate.Create(organizationId, "Concurrent access", aliceId).Value;
        Assert.False(organization.AddMember(bobId, [OrganizationRole.Administrator]).IsError);
        var project = ProjectAggregate.Create(projectId, organizationId, "Shop", "shop", aliceId).Value;
        Assert.False(project.AssignRole(bobId, ProjectRole.Owner).IsError);

        await using var setupContext = postgres.CreatePersistenceDbContext(new TestCurrentOrganization(organizationId));
        await setupContext.Database.EnsureCreatedAsync();
        setupContext.Organizations.Add(organization);
        setupContext.Projects.Add(project);
        await setupContext.SaveChangesAsync();

        await using var membershipContext = postgres.CreatePersistenceDbContext(new TestCurrentOrganization(organizationId));
        await using var roleContext = postgres.CreatePersistenceDbContext(new TestCurrentOrganization(organizationId));
        var membershipOrganization = await membershipContext.Organizations
            .Include(candidate => candidate.Members)
            .SingleAsync(candidate => candidate.Id == organizationId);
        var roleOrganization = await roleContext.Organizations
            .SingleAsync(candidate => candidate.Id == organizationId);
        var roleProject = await roleContext.Projects
            .Include(candidate => candidate.RoleAssignments)
            .SingleAsync(candidate => candidate.Id == projectId);

        Assert.False(membershipOrganization.RemoveMember(aliceId).IsError);
        await membershipContext.SaveChangesAsync();

        Assert.False(roleProject.RemoveRole(bobId, ProjectRole.Owner).IsError);
        roleOrganization.RecordProjectAuthorizationChange();
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => roleContext.SaveChangesAsync());

        await using var verificationContext = postgres.CreatePersistenceDbContext(new TestCurrentOrganization(organizationId));
        var remainingProjectOwners = await verificationContext.Projects
            .Where(candidate => candidate.Id == projectId)
            .SelectMany(candidate => candidate.RoleAssignments)
            .CountAsync(assignment => assignment.Role == ProjectRole.Owner);
        Assert.Equal(2, remainingProjectOwners);
    }

    private sealed class TestCurrentOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }
}
