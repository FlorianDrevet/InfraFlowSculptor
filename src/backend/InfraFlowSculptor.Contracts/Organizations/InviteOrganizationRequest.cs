using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Contracts.Organizations;

public sealed record InviteOrganizationRequest(string Email, IReadOnlyCollection<OrganizationRole> Roles);
