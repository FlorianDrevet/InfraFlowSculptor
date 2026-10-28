using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Contracts.Organizations;

public sealed record OrganizationMemberResponse(
    Guid Id,
    string DisplayName,
    string? Email,
    IReadOnlyCollection<OrganizationRole> Roles,
    bool IsOutsideAccessRestrictions);
