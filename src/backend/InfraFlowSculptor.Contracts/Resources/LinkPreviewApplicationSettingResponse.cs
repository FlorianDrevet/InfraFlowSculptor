namespace InfraFlowSculptor.Contracts.Resources;

public sealed record LinkPreviewApplicationSettingResponse(
    Guid EnvironmentId,
    string EnvironmentCode,
    Guid ApplicationResourceId,
    string ApplicationResourceName,
    string Name,
    string Origin,
    bool IsSensitive);
