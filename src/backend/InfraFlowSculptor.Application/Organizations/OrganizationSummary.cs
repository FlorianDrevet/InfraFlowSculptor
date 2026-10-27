using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Application.Organizations;

public sealed record OrganizationSummary(
    Guid Id,
    string Name,
    IReadOnlyCollection<OrganizationRole> Roles,
    int Version,
    IReadOnlyCollection<string> AllowedEmailDomains,
    IReadOnlyCollection<Guid> AllowedTenantIds);
