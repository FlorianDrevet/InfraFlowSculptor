namespace InfraFlowSculptor.Contracts.Naming;

public sealed record ProjectNamingSettingsRequest(
    string Preset,
    string DefaultTemplate,
    string ResourceGroupTemplate,
    IReadOnlyList<NamingOverrideRequest> TypeOverrides,
    int ExpectedVersion);
