namespace InfraFlowSculptor.Engine.Links;

public sealed record ImplicitApplicationSetting(
    Guid Id,
    Guid ApplicationResourceId,
    string Name,
    Guid? SourceResourceId,
    string? OutputName,
    string? Value,
    bool IsSensitive,
    string Origin);
