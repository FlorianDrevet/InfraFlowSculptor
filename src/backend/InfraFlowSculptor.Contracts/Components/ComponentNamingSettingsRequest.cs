using InfraFlowSculptor.Contracts.Naming;

namespace InfraFlowSculptor.Contracts.Components;

public sealed record ComponentNamingSettingsRequest(
    string? DefaultTemplate,
    IReadOnlyList<NamingOverrideRequest> TypeOverrides,
    int ExpectedVersion);
