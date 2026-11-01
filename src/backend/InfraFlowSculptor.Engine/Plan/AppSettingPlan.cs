namespace InfraFlowSculptor.Engine.Plan;

public sealed record AppSettingPlan(
    string Name,
    string Destination,
    string Source,
    string? Value,
    string? SourceResource,
    string? OutputName,
    string? KeyVault,
    string? SecretName,
    string? Identity,
    string? SecretFeedKind,
    string? PipelineVariableName,
    bool IsImplicit,
    bool IsSensitive);
