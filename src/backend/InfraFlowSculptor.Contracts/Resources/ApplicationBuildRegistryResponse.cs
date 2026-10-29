namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ApplicationBuildRegistryResponse(
    bool Available,
    string? DisplayName,
    string? RegistryName,
    bool PromotionRequired,
    Guid? SelectedEnvironmentId,
    bool HasValidSelection,
    IReadOnlyList<ApplicationBuildRegistryOptionResponse> Options);
