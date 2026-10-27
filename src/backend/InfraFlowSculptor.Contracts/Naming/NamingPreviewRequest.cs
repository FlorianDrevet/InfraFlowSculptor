namespace InfraFlowSculptor.Contracts.Naming;

public sealed record NamingPreviewRequest(
    string ProjectName,
    string ProjectCode,
    string NamingPreset,
    string DefaultNameTemplate,
    string ResourceGroupNameTemplate,
    IReadOnlyList<NamingPreviewEnvironmentRequest> Environments,
    IReadOnlyDictionary<string, string>? Tags = null);
