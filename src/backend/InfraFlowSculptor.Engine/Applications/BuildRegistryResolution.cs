namespace InfraFlowSculptor.Engine.Applications;

public sealed record BuildRegistryResolution(
    bool IsAvailable,
    string? ComponentCode,
    string? ResourceLogicalName,
    string? RegistryName,
    bool PromotionRequired,
    Guid? SelectedEnvironmentId,
    IReadOnlyList<BuildRegistryOption> Options,
    bool HasValidSelection)
{
    public string? DisplayName => IsAvailable && ComponentCode is not null && ResourceLogicalName is not null
        ? $"{ComponentCode} / {ResourceLogicalName}"
        : null;
}
