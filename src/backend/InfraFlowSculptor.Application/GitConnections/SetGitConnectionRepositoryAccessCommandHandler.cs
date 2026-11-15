using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Common.Secrets;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Application.GitConnections;

public sealed class SetGitConnectionRepositoryAccessCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    GitProviderRegistry providers,
    ISecretStore secretStore) :
    ICommandHandler<SetGitConnectionRepositoryAccessCommand, ErrorOr<GitConnectionSummary>>
{
    public async ValueTask<ErrorOr<GitConnectionSummary>> Handle(
        SetGitConnectionRepositoryAccessCommand command,
        CancellationToken cancellationToken)
    {
        var organizationId = new OrganizationId(command.OrganizationId);
        if (organizationId.Value == Guid.Empty || currentOrganization.Id != organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var projectIds = command.ProjectIds.Select(value => new ProjectId(value)).Distinct().ToArray();
        if (!command.OpenToAllProjects)
        {
            var validProjectCount = await dbContext.Query<ProjectAggregate>()
                .CountAsync(
                    project => project.OrganizationId == organizationId && projectIds.Contains(project.Id),
                    cancellationToken);
            if (validProjectCount != projectIds.Length)
            {
                return Error.NotFound("NOT_FOUND", "One or more projects were not found in this organization.");
            }
        }

        var connection = await dbContext.Query<GitConnection>()
            .Include(candidate => candidate.Repositories)
            .ThenInclude(repository => repository.AllowedProjects)
            .SingleOrDefaultAsync(candidate => candidate.Id == new GitConnectionId(command.ConnectionId), cancellationToken);
        if (connection is null)
        {
            return Error.NotFound("NOT_FOUND", "The Git connection was not found.");
        }

        if (connection.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The Git connection changed. Reload it and try again.");
        }

        string? accessToken = connection.UsesFallbackToken && connection.SecretName is { } secretName
            ? await secretStore.GetAsync(secretName, cancellationToken)
            : null;
        if (!providers.Supports(connection.ProviderKind)
            || (connection.UsesFallbackToken && string.IsNullOrWhiteSpace(accessToken)))
        {
            return ConnectionTestFailed();
        }

        IReadOnlyCollection<GitRepositoryInfo> repositories;
        try
        {
            repositories = await providers.Get(connection.ProviderKind).GetRepositoriesAsync(
                connection,
                accessToken,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ConnectionTestFailed();
        }

        var replaced = connection.ReplaceRepositories(repositories.Select(repository =>
            GitConnectionRepository.Create(
                repository.ProviderRepositoryId,
                repository.Name,
                repository.DefaultBranch)));
        if (replaced.IsError)
        {
            return replaced.Errors;
        }

        var changed = command.OpenToAllProjects
            ? connection.OpenRepositoryToAllProjects(command.ProviderRepositoryId)
            : projectIds.Length == 0
                ? connection.CloseRepository(command.ProviderRepositoryId)
                : connection.OpenRepositoryToProjects(command.ProviderRepositoryId, projectIds);
        return changed.IsError ? changed.Errors : GitConnectionSummaryMapper.Map(connection);
    }

    private static Error ConnectionTestFailed() =>
        Error.Validation("VAL-PUB-CONNEXION", "La connexion au fournisseur Git a échoué ; les changements n’ont pas été enregistrés.");
}
