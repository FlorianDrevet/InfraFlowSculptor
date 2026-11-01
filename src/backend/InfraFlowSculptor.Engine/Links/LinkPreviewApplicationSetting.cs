namespace InfraFlowSculptor.Engine.Links;

public sealed record LinkPreviewApplicationSetting(
    Guid EnvironmentId,
    string EnvironmentCode,
    Guid ApplicationResourceId,
    string ApplicationResourceName,
    string Name,
    string Origin,
    bool IsSensitive);
