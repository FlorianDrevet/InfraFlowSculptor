using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Common.Secrets;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Application.GitConnections;

public sealed class TestGitConnectionCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    GitProviderRegistry providers,
    ISecretStore secretStore) : ICommandHandler<TestGitConnectionCommand, ErrorOr<GitConnectionSummary>>
{
    public async ValueTask<ErrorOr<GitConnectionSummary>> Handle(
        TestGitConnectionCommand command,
        CancellationToken cancellationToken)
    {
        var organizationId = new OrganizationId(command.OrganizationId);
        if (organizationId.Value == Guid.Empty || currentOrganization.Id != organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var connection = await dbContext.Query<GitConnection>()
            .Include(candidate => candidate.Repositories)
            .ThenInclude(repository => repository.AllowedProjects)
            .SingleOrDefaultAsync(candidate => candidate.Id == new GitConnectionId(command.ConnectionId), cancellationToken);
        if (connection is null)
        {
            return Error.NotFound("NOT_FOUND", "The Git connection was not found.");
        }

        string? accessToken = null;
        if (connection.UsesFallbackToken)
        {
            accessToken = connection.SecretName is null
                ? null
                : await secretStore.GetAsync(connection.SecretName, cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return ConnectionTestFailed();
            }
        }

        if (!providers.Supports(connection.ProviderKind))
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
        return replaced.IsError ? replaced.Errors : GitConnectionSummaryMapper.Map(connection);
    }

    private static Error ConnectionTestFailed() =>
        Error.Validation("VAL-PUB-CONNEXION", "La connexion au fournisseur Git a échoué.");
}
