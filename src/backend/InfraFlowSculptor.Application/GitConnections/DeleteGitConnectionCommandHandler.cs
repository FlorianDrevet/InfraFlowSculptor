using ErrorOr;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

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

        var connection = await dbContext.Query<GitConnection>()
            .SingleOrDefaultAsync(candidate => candidate.Id == connectionId, cancellationToken);
        if (connection is null)
        {
            return Error.NotFound("NOT_FOUND", "The Git connection was not found.");
        }

        var projectsUsingConnection = await (
                from usage in dbContext.Query<GitConnectionPublicationUse>()
                join project in dbContext.Query<ProjectAggregate>() on usage.ProjectId equals project.Id
                where usage.ConnectionId == connectionId
                orderby project.Name
                select project.Name)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (projectsUsingConnection.Length > 0)
        {
            var projectNames = string.Join(", ", projectsUsingConnection);
            return Error.Conflict(
                "CONFLICT_GIT_CONNECTION_IN_USE",
                $"La connexion est utilisée par les projets suivants : {projectNames}.");
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
