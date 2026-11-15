using InfraFlowSculptor.Domain.GitConnectionAggregate;

namespace InfraFlowSculptor.Application.GitConnections;

public sealed record GitConnectionSummary(
    Guid Id,
    GitProviderKind ProviderKind,
    string Name,
    string Organization,
    Guid? TenantId,
    GitConnectionAuthenticationMode AuthenticationMode,
    DateTimeOffset? PersonalAccessTokenExpiresAt,
    int Version,
    IReadOnlyCollection<GitConnectionRepositorySummary> Repositories,
    IReadOnlyCollection<string> UsedByProjectNames);
