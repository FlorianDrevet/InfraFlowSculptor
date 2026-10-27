namespace InfraFlowSculptor.Contracts.Projects;

public sealed record CreateProjectFromWizardRequest(
    string Name,
    string Code,
    Guid TenantId,
    string Description,
    string NamingPreset,
    string DefaultNameTemplate,
    string ResourceGroupNameTemplate,
    IReadOnlyList<ProjectWizardEnvironmentRequest> Environments,
    IReadOnlyDictionary<string, string>? Tags = null,
    string? PublishingPreset = null,
    string? CatalogVersion = null);
