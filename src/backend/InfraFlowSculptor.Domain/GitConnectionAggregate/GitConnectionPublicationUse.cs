using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.GitConnectionAggregate;

public sealed record GitConnectionPublicationUse(
    OrganizationId OrganizationId,
    GitConnectionId ConnectionId,
    ProjectId ProjectId) : IOrganizationOwned;
