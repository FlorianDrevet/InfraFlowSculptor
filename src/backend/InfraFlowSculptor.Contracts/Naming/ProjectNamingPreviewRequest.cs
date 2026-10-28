namespace InfraFlowSculptor.Contracts.Naming;

public sealed record ProjectNamingPreviewRequest(
    string Preset,
    string DefaultTemplate,
    string ResourceGroupTemplate,
    IReadOnlyList<NamingOverrideRequest> TypeOverrides);
