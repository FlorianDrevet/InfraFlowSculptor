using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Infrastructure.Authentication;

public sealed class HttpCurrentOrganization : ICurrentOrganization
{
    public OrganizationId? Id { get; private set; }

    public void Set(OrganizationId? organizationId) => Id = organizationId;
}
