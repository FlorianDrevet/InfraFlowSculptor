using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.OrganizationAggregate;

[AuditedObject(
    ObjectType.ProjectRoleAssignment,
    nameof(ProjectRoleAssignment.Principal),
    AuditObjectPropertyNames.OrganizationId)]
public sealed class ProjectRoleAssignment
{
    public OrganizationId OrganizationId { get; private set; }

    public UserProfileId Principal { get; private set; }

    public ProjectRole Role { get; private set; }

    public ProjectRoleScope Scope { get; private set; }

    private ProjectRoleAssignment()
    {
    }

    internal ProjectRoleAssignment(
        OrganizationId organizationId,
        UserProfileId principal,
        ProjectRole role,
        ProjectRoleScope scope)
    {
        OrganizationId = organizationId;
        Principal = principal;
        Role = role;
        Scope = scope;
    }
}
