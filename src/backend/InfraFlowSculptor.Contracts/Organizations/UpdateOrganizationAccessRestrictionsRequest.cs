namespace InfraFlowSculptor.Contracts.Organizations;

public sealed record UpdateOrganizationAccessRestrictionsRequest(
    IReadOnlyCollection<string> AllowedEmailDomains,
    IReadOnlyCollection<Guid> AllowedTenantIds);
