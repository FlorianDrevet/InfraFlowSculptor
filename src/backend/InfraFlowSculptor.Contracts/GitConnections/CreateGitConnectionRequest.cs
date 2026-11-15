using InfraFlowSculptor.Domain.GitConnectionAggregate;

namespace InfraFlowSculptor.Contracts.GitConnections;

public sealed record CreateGitConnectionRequest(
    GitProviderKind ProviderKind,
    string Name,
    string Organization,
    Guid? TenantId,
    GitConnectionAuthenticationMode AuthenticationMode,
    DateTimeOffset? PersonalAccessTokenExpiresAt,
    string? PersonalAccessToken);
