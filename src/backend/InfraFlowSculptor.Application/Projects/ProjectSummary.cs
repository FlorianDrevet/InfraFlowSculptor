namespace InfraFlowSculptor.Application.Projects;

public sealed record ProjectSummary(
    Guid Id,
    int Version,
    string Name,
    string Code,
    string Description,
    int ModelVersion,
    int EnvironmentCount,
    DateTimeOffset? FirstPublishedAt,
    DateTimeOffset? DeletedAt,
    Guid TenantId,
    string InfrastructureLanguage,
    string CiPlatform,
    string ModuleSource,
    string CatalogVersion,
    bool SystemTagsEnabled,
    Guid? CoordinatorUserProfileId,
    IReadOnlyList<string> DefaultRunners,
    IReadOnlyList<string> DefaultReviewers,
    IReadOnlyDictionary<string, string> Tags);
