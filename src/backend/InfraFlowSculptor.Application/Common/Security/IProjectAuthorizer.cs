using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Application.Common.Security;

public interface IProjectAuthorizer
{
    Task<ProjectAuthorizationContext?> GetProjectAuthorizationContextAsync(
        UserKey userKey,
        OrganizationId organizationId,
        ProjectId projectId,
        CancellationToken cancellationToken);

    Task<bool> CanAssignRoleAsync(
        UserKey userKey,
        OrganizationId organizationId,
        ProjectId projectId,
        ProjectRole role,
        CancellationToken cancellationToken);
}
