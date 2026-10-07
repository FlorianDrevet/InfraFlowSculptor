using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Security;

public interface ICurrentOrganization
{
    OrganizationId? Id { get; }
}
