namespace InfraFlowSculptor.Engine.Plan;

public sealed record ProjectInfo(
    string Name,
    string Code,
    string Description,
    string InfrastructureLanguage,
    string CiPlatform,
    string ModuleSource,
    string? PublishingPreset);
