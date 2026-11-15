using InfraFlowSculptor.Domain.GitConnectionAggregate;

namespace InfraFlowSculptor.Contracts.GitConnections;

public sealed record GitConnectionResponse(
    Guid Id,
    GitProviderKind ProviderKind,
    string Name,
    string Organization,
    Guid? TenantId,
    GitConnectionAuthenticationMode AuthenticationMode,
    DateTimeOffset? PersonalAccessTokenExpiresAt,
    int Version,
    IReadOnlyCollection<GitConnectionRepositoryResponse> Repositories,
    IReadOnlyCollection<string> UsedByProjectNames);
