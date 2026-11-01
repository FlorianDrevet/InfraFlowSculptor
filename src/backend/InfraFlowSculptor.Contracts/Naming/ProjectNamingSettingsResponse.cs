namespace InfraFlowSculptor.Contracts.Naming;

public sealed record ProjectNamingSettingsResponse(
    Guid ProjectId,
    int Version,
    string Preset,
    string DefaultTemplate,
    string ResourceGroupTemplate,
    IReadOnlyList<NamingOverrideResponse> TypeOverrides,
    bool IsPublished);
