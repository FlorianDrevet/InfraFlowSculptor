using InfraFlowSculptor.Domain.GitConnectionAggregate;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Application.GitConnections;

public static class GitConnectionSummaryMapper
{
    public static GitConnectionSummary Map(
        GitConnection connection,
        IReadOnlyCollection<string>? usedByProjectNames = null) => new(
        connection.Id.Value,
        connection.ProviderKind,
        connection.Name,
        connection.Organization,
        connection.TenantId,
        connection.AuthenticationMode,
        connection.PersonalAccessTokenExpiresAt,
        connection.Version,
        connection.Repositories.Select(repository => new GitConnectionRepositorySummary(
            repository.ProviderRepositoryId,
            repository.Name,
            repository.DefaultBranch,
            repository.OpenToAllProjects,
            repository.AllowedProjects.Select(access => access.ProjectId).ToArray())).ToArray(),
        usedByProjectNames ?? Array.Empty<string>());
}
