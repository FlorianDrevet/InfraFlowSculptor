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

public sealed class UpdateGitConnectionCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    GitProviderRegistry providers,
    ISecretStore secretStore,
    TimeProvider clock) : ICommandHandler<UpdateGitConnectionCommand, ErrorOr<GitConnectionSummary>>
{
    public async ValueTask<ErrorOr<GitConnectionSummary>> Handle(
        UpdateGitConnectionCommand command,
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

        if (connection.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The Git connection changed. Reload it and try again.");
        }

        var updated = GitConnection.Create(
            connection.Id,
            organizationId,
            connection.ProviderKind,
            command.Name,
            command.Organization,
            command.TenantId,
            connection.AuthenticationMode,
            command.PersonalAccessTokenExpiresAt,
            connection.SecretName,
            clock.GetUtcNow());
        if (updated.IsError)
        {
            return updated.Errors;
        }

        string? accessToken = command.PersonalAccessToken;
        if (connection.UsesFallbackToken && string.IsNullOrWhiteSpace(accessToken))
        {
            accessToken = connection.SecretName is null
                ? null
                : await secretStore.GetAsync(connection.SecretName, cancellationToken);
        }

        if (connection.UsesFallbackToken && string.IsNullOrWhiteSpace(accessToken))
        {
            return ConnectionTestFailed();
        }

        if (!providers.Supports(connection.ProviderKind))
        {
            return ConnectionTestFailed();
        }

        IReadOnlyCollection<GitRepositoryInfo> repositories;
        try
        {
            repositories = await providers.Get(connection.ProviderKind).GetRepositoriesAsync(
                updated.Value,
                accessToken,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ConnectionTestFailed();
        }

        var updatedDetails = connection.UpdateDetails(
            command.Name,
            command.Organization,
            command.TenantId,
            command.PersonalAccessTokenExpiresAt,
            clock.GetUtcNow());
        if (updatedDetails.IsError)
        {
            return updatedDetails.Errors;
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

        if (connection.UsesFallbackToken
            && !string.IsNullOrWhiteSpace(command.PersonalAccessToken)
            && connection.SecretName is { } secretName)
        {
            await secretStore.SetAsync(secretName, command.PersonalAccessToken, cancellationToken);
        }

        return GitConnectionSummaryMapper.Map(connection);
    }

    private static Error ConnectionTestFailed() =>
        Error.Validation("VAL-PUB-CONNEXION", "La connexion au fournisseur Git a échoué ; les changements n’ont pas été enregistrés.");
}
