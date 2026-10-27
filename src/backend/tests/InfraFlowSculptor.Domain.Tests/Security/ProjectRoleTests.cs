using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Xunit;

namespace InfraFlowSculptor.Domain.Tests.Security;

public sealed class ProjectRoleTests
{
    [Fact]
    public void ProjectRolesMatchThePilotPermissionMatrix()
    {
        var expected = new Dictionary<ProjectRole, string[]>
        {
            [ProjectRole.Reader] = [Permission.ProjectRead],
            [ProjectRole.Auditor] = [Permission.ProjectRead, Permission.AuditRead],
            [ProjectRole.Developer] =
            [
                Permission.ProjectRead,
                Permission.ModelEdit,
                Permission.ApplicationsManage,
                Permission.Generate
            ],
            [ProjectRole.Contributor] =
            [
                Permission.ProjectRead,
                Permission.ModelEdit,
                Permission.ApplicationsManage,
                Permission.ComponentsManage,
                Permission.Generate,
                Permission.Publish,
                Permission.ApplyProposals
            ],
            [ProjectRole.DeploymentManager] =
            [
                Permission.ProjectRead,
                Permission.Generate,
                Permission.Publish,
                Permission.InstallationManage,
                Permission.EnvironmentsManage
            ],
            [ProjectRole.PlatformArchitect] =
            [
                Permission.ProjectRead,
                Permission.ConventionsManage,
                Permission.ComponentsManage,
                Permission.ModelEdit,
                Permission.ApplicationsManage,
                Permission.EnvironmentsManage,
                Permission.ProtectedEnvironmentsManage,
                Permission.PublicationManage,
                Permission.Generate,
                Permission.Publish,
                Permission.InstallationManage,
                Permission.ApplyProposals
            ],
            [ProjectRole.ProjectAdministrator] = Permission.All
                .Where(permission => permission != Permission.ProjectAdminister)
                .ToArray(),
            [ProjectRole.Owner] = Permission.All.ToArray()
        };

        Assert.Equal(expected.Keys.Order(), Enum.GetValues<ProjectRole>().Order());
        foreach (var (role, expectedPermissions) in expected)
        {
            Assert.Equal(expectedPermissions.Order(StringComparer.Ordinal), ProjectRoles.PermissionsFor(role).Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void AssigningRoleRequiresAllPermissionsAndOwnerForOwner()
    {
        var contributorPermissions = ProjectRoles.PermissionsFor(ProjectRole.Contributor);

        Assert.True(ProjectRoles.CanAssign(contributorPermissions, actorIsOwner: false, ProjectRole.Reader));
        Assert.False(ProjectRoles.CanAssign(contributorPermissions, actorIsOwner: false, ProjectRole.ProjectAdministrator));
        Assert.False(ProjectRoles.CanAssign(ProjectRoles.PermissionsFor(ProjectRole.Owner), actorIsOwner: false, ProjectRole.Owner));
        Assert.True(ProjectRoles.CanAssign(ProjectRoles.PermissionsFor(ProjectRole.Owner), actorIsOwner: true, ProjectRole.Owner));
    }

    [Fact]
    public void OwnerAssignmentRequiresTheOwnerOnlyPermission()
    {
        var missingPermission = ProjectRoles.MissingPermissionToAssign(
            Permission.All.ToHashSet(StringComparer.Ordinal),
            actorIsOwner: false,
            ProjectRole.Owner);

        Assert.Equal(Permission.ProjectAdminister, missingPermission);
    }
}
