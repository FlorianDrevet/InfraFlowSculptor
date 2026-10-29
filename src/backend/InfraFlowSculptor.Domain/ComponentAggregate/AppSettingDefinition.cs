namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed record AppSettingDefinition(
    string Name,
    AppSettingSourceKind SourceKind,
    string? LiteralValue = null,
    IReadOnlyDictionary<Guid, string?>? LiteralOverrides = null,
    Guid? SourceResourceId = null,
    string? OutputName = null,
    Guid? KeyVaultResourceId = null,
    string? SecretName = null,
    AppSettingSecretFeedKind? SecretFeedKind = null,
    string? PipelineVariableName = null,
    Guid? IdentityResourceId = null,
    bool ForceNonSecret = false);
