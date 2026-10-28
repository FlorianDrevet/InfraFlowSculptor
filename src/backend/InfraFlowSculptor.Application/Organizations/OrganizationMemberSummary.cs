using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Application.Organizations;

public sealed record OrganizationMemberSummary(
    Guid Id,
    string DisplayName,
    string? Email,
    IReadOnlyCollection<OrganizationRole> Roles,
    bool IsOutsideAccessRestrictions);
