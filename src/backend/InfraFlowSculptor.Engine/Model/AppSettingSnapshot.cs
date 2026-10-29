using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Engine.Model;

public sealed record AppSettingSnapshot(
    Guid Id,
    Guid ComponentId,
    Guid ApplicationResourceId,
    string Name,
    AppSettingSourceKind SourceKind,
    string? LiteralValue,
    IReadOnlyDictionary<Guid, string> LiteralOverrides,
    Guid? SourceResourceId,
    string? OutputName,
    Guid? KeyVaultResourceId,
    string? SecretName,
    AppSettingSecretFeedKind? SecretFeedKind,
    string? PipelineVariableName,
    Guid? IdentityResourceId);
