using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Infrastructure.Authentication;

public sealed class HttpCurrentOrganization(ICurrentUser currentUser) : ICurrentOrganization
{
    public OrganizationId? Id => currentUser.IsAuthenticated && currentUser.Key.TenantId != Guid.Empty
        ? new OrganizationId(currentUser.Key.TenantId)
        : null;
}
