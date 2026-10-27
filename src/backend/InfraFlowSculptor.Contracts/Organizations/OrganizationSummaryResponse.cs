using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Contracts.Organizations;

public sealed record OrganizationSummaryResponse(
    Guid Id,
    string Name,
    IReadOnlyCollection<OrganizationRole> Roles,
    int Version,
    IReadOnlyCollection<string> AllowedEmailDomains,
    IReadOnlyCollection<Guid> AllowedTenantIds);
