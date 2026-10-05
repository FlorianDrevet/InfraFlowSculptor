using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Domain.Common.Models;

public interface IOrganizationOwned
{
    OrganizationId OrganizationId { get; }
}
