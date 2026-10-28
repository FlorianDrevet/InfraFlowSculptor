using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Contracts.Organizations;

public sealed record ChangeOrganizationMemberRolesRequest(IReadOnlyCollection<OrganizationRole> Roles);
