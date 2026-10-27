namespace InfraFlowSculptor.Contracts.Projects;

public sealed record CreateProjectRequest(
    string Name,
    string Code,
    Guid TenantId,
    string Description = "",
    Guid? CoordinatorUserProfileId = null,
    string? CatalogVersion = null,
    IReadOnlyList<string>? DefaultRunners = null,
    IReadOnlyList<string>? DefaultReviewers = null);
