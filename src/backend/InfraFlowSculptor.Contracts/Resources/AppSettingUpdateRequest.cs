using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record AppSettingUpdateRequest(
    string Name,
    AppSettingSourceKind SourceKind,
    string? LiteralValue,
    IReadOnlyDictionary<Guid, string?>? Overrides,
    Guid? SourceResourceId,
    string? OutputName,
    Guid? KeyVaultResourceId,
    string? SecretName,
    AppSettingSecretFeedKind? SecretFeedKind,
    string? PipelineVariableName,
    Guid? IdentityResourceId,
    bool ForceNonSecret,
    int ExpectedVersion);
