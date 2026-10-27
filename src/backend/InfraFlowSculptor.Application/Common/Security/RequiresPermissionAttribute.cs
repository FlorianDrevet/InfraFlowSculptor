using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using ProjectPermission = InfraFlowSculptor.Domain.Common.Security.Permission;

namespace InfraFlowSculptor.Application.Common.Security;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = ProjectPermission.All.Contains(permission, StringComparer.Ordinal)
        ? permission
        : throw new ArgumentOutOfRangeException(nameof(permission), permission, "The project permission is not supported.");

    public ProjectRoleScope Scope { get; set; } = ProjectRoleScope.Project;
}
