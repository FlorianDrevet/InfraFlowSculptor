using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Application.Common.Security;

public interface IOrganizationRoleAuthorizer
{
    Task<bool> HasRoleAsync(
        UserKey userKey,
        OrganizationId organizationId,
        OrganizationRole requiredRole,
        CancellationToken cancellationToken);
}
