using ErrorOr;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.PublicationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;
using Publication = InfraFlowSculptor.Domain.PublicationAggregate.PublicationAggregate;

namespace InfraFlowSculptor.Application.GitConnections;

public sealed class DeleteGitConnectionCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    IJobDispatcher jobDispatcher) : ICommandHandler<DeleteGitConnectionCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(
        DeleteGitConnectionCommand command,
        CancellationToken cancellationToken)
    {
        var organizationId = new OrganizationId(command.OrganizationId);
        var connectionId = new GitConnectionId(command.ConnectionId);
        if (organizationId.Value == Guid.Empty || currentOrganization.Id != organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        if (!await dbContext.LockGitConnectionForUpdateAsync(connectionId, cancellationToken))
        {
            return Error.NotFound("NOT_FOUND", "The Git connection was not found.");
        }

        var connection = await dbContext.Query<GitConnection>()
            .SingleOrDefaultAsync(candidate => candidate.Id == connectionId, cancellationToken);
        if (connection is null)
        {
            return Error.NotFound("NOT_FOUND", "The Git connection was not found.");
        }

        var projectsUsingUsage = await (
                from usage in dbContext.Query<GitConnectionPublicationUse>()
                join project in dbContext.Query<ProjectAggregate>() on usage.ProjectId equals project.Id
                where usage.ConnectionId == connectionId
                select project.Name)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var projectsWithPublicationPlan = await dbContext.Query<ProjectAggregate>()
            .Where(project => project.PublicationPlan.Destinations
                .Any(destination => destination.GitConnectionId == connectionId))
            .Select(project => project.Name)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var projectsWithActivePublications = await dbContext.Query<Publication>()
            .Where(publication =>
                (publication.State == PublicationState.Preparing
                    || publication.State == PublicationState.NeedsConfirmation
                    || publication.State == PublicationState.Prepared
                    || publication.State == PublicationState.Publishing)
                && publication.Targets.Any(target => target.GitConnectionId == connectionId))
            .Join(
                dbContext.Query<ProjectAggregate>(),
                publication => publication.ProjectId,
                project => project.Id,
                (_, project) => project.Name)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var projectNames = projectsUsingUsage
            .Concat(projectsWithPublicationPlan)
            .Concat(projectsWithActivePublications)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (projectNames.Length > 0)
        {
            var names = string.Join(", ", projectNames);
            return Error.Conflict(
                "CONFLICT_GIT_CONNECTION_IN_USE",
                $"La connexion est utilisée par les projets suivants : {names}.");
        }

        dbContext.Query<GitConnection>().Remove(connection);
        if (connection.SecretName is { } secretName)
        {
            jobDispatcher.Enqueue(
                JobQueue.Notifications,
                organizationId,
                new DeleteGitConnectionSecretJob(secretName));
        }

        return Result.Success;
    }
}
