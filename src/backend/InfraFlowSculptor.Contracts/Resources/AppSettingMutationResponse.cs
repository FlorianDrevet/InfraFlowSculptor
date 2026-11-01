using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record AppSettingMutationResponse(
    Guid Id,
    int Version,
    string Name,
    AppSettingSourceKind SourceKind,
    Guid? SourceResourceId,
    string? OutputName,
    Guid? KeyVaultResourceId,
    string? SecretName,
    AppSettingSecretFeedKind? SecretFeedKind,
    string? PipelineVariableName,
    Guid? IdentityResourceId,
    bool SecretValueDeclaredNonSecret,
    IReadOnlyList<string> Warnings,
    bool IsImplicit,
    string? Origin);
