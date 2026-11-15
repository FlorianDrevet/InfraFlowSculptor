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

public sealed class CreateGitConnectionCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    GitProviderRegistry providers,
    ISecretStore secretStore,
    ITransactionRollbackActions rollbackActions,
    TimeProvider clock) : ICommandHandler<CreateGitConnectionCommand, ErrorOr<GitConnectionSummary>>
{
    public async ValueTask<ErrorOr<GitConnectionSummary>> Handle(
        CreateGitConnectionCommand command,
        CancellationToken cancellationToken)
    {
        var organizationId = new OrganizationId(command.OrganizationId);
        if (organizationId.Value == Guid.Empty || currentOrganization.Id != organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var secretName = command.AuthenticationMode == GitConnectionAuthenticationMode.PersonalAccessToken
            ? GitConnectionSecretNames.CreatePersonalAccessTokenName(command.ConnectionId)
            : null;
        var created = GitConnection.Create(
            new GitConnectionId(command.ConnectionId),
            organizationId,
            command.ProviderKind,
            command.Name,
            command.Organization,
            command.TenantId,
            command.AuthenticationMode,
            command.PersonalAccessTokenExpiresAt,
            secretName,
            clock.GetUtcNow());
        if (created.IsError)
        {
            return created.Errors;
        }

        if (!providers.Supports(command.ProviderKind))
        {
            return ConnectionTestFailed();
        }

        IReadOnlyCollection<GitRepositoryInfo> repositories;
        try
        {
            repositories = await providers.Get(command.ProviderKind).GetRepositoriesAsync(
                created.Value,
                command.PersonalAccessToken,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ConnectionTestFailed();
        }

        var replaced = created.Value.ReplaceRepositories(repositories.Select(repository =>
            GitConnectionRepository.Create(
                repository.ProviderRepositoryId,
                repository.Name,
                repository.DefaultBranch)));
        if (replaced.IsError)
        {
            return replaced.Errors;
        }

        if (secretName is not null && command.PersonalAccessToken is { } token)
        {
            rollbackActions.Register(cancellationToken => secretStore.DeleteAsync(secretName, cancellationToken));
            await secretStore.SetAsync(secretName, token, cancellationToken);
        }

        await dbContext.Query<GitConnection>().AddAsync(created.Value, cancellationToken);
        return GitConnectionSummaryMapper.Map(created.Value);
    }

    private static Error ConnectionTestFailed() =>
        Error.Validation("VAL-PUB-CONNEXION", "La connexion au fournisseur Git a échoué ; elle n’a pas été enregistrée.");
}
