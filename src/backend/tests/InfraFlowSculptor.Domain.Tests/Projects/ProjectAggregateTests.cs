using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Project = InfraFlowSculptor.Domain.ProjectAggregate.ProjectAggregate;
using Xunit;

namespace InfraFlowSculptor.Domain.Tests.Projects;

public sealed class ProjectAggregateTests
{
    [Fact]
    public void CreateAssignsCreatorAsNamedOwner()
    {
        var creatorId = new UserProfileId(Guid.NewGuid());
        var project = CreateProject(creatorId);

        Assert.True(project.HasRole(creatorId, ProjectRole.Owner));
        Assert.Contains(Permission.ProjectAdminister, project.EffectivePermissions(creatorId));
    }

    [Fact]
    public void RemovingLastNamedOwnerFailsWithRgOrg11()
    {
        var ownerId = new UserProfileId(Guid.NewGuid());
        var project = CreateProject(ownerId);

        var result = project.RemoveRole(ownerId, ProjectRole.Owner);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "RG-ORG-11");
        Assert.True(project.HasRole(ownerId, ProjectRole.Owner));
    }

    [Fact]
    public void TakeOwnershipAddsOwnerWithoutRemovingCurrentOwnerAndIsIdempotent()
    {
        var currentOwnerId = new UserProfileId(Guid.NewGuid());
        var organizationAdministratorId = new UserProfileId(Guid.NewGuid());
        var project = CreateProject(currentOwnerId);

        Assert.False(project.TakeOwnership(organizationAdministratorId).IsError);
        Assert.False(project.TakeOwnership(organizationAdministratorId).IsError);

        Assert.True(project.HasRole(currentOwnerId, ProjectRole.Owner));
        Assert.True(project.HasRole(organizationAdministratorId, ProjectRole.Owner));
        Assert.Equal(2, project.RoleAssignments.Count(assignment => assignment.Role == ProjectRole.Owner));
    }

    [Fact]
    public void EffectivePermissionsUnionsRolesAndIncludesImplicitProjectRead()
    {
        var ownerId = new UserProfileId(Guid.NewGuid());
        var memberId = new UserProfileId(Guid.NewGuid());
        var project = CreateProject(ownerId);
        Assert.False(project.AssignRole(memberId, ProjectRole.Reader).IsError);
        Assert.False(project.AssignRole(memberId, ProjectRole.Auditor).IsError);

        var permissions = project.EffectivePermissions(memberId);

        Assert.Equal(
            new[] { Permission.ProjectRead, Permission.AuditRead }.Order(StringComparer.Ordinal),
            permissions.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void UnsupportedScopeCannotBeAssigned()
    {
        var ownerId = new UserProfileId(Guid.NewGuid());
        var project = CreateProject(ownerId);

        var result = project.AssignRole(new UserProfileId(Guid.NewGuid()), ProjectRole.Reader, (ProjectRoleScope)99);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "PROJECT_ROLE_INVALID");
    }

    private static Project CreateProject(UserProfileId ownerId) =>
        Project.Create(
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            "Shop",
            "shop",
            ownerId).Value;
}
