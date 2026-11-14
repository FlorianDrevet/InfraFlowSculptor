using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Publication;

public sealed class SetProjectPublishPlanCommandHandler(IIfsDbContext dbContext)
    : ICommandHandler<SetProjectPublishPlanCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        SetProjectPublishPlanCommand command,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.PublicationPlan.Destinations)
            .Include(candidate => candidate.PublicationPlan.Assignments)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(command.ProjectId), cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        if (project.Version != command.ExpectedVersion)
        {
            return Error.Conflict(
                "CONFLICT_VERSION",
                "Le projet a changé. Rechargez-le avant de modifier son plan de publication.",
                new Dictionary<string, object> { ["currentVersion"] = project.Version });
        }

        var connectionIds = command.Destinations
            .Select(destination => new GitConnectionId(destination.GitConnectionId))
            .Distinct()
            .ToArray();
        var connections = await dbContext.Query<GitConnectionAggregate>()
            .Include(connection => connection.Repositories)
            .ThenInclude(repository => repository.AllowedProjects)
            .Where(connection => connectionIds.Contains(connection.Id))
            .ToDictionaryAsync(connection => connection.Id, cancellationToken);

        foreach (var destination in command.Destinations)
        {
            var connectionId = new GitConnectionId(destination.GitConnectionId);
            if (!connections.TryGetValue(connectionId, out var connection)
                || !connection.CanAccessRepository(destination.ProviderRepositoryId, project.Id))
            {
                return Error.Validation(
                    PublishValidationCodes.Destination,
                    "Le dépôt n’est pas accessible à ce projet.");
            }
        }

        var configured = project.ConfigurePublicationPlan(new PublishPlanConfiguration(
            command.Preset,
            command.Destinations,
            command.Assignments,
            command.DefaultInfrastructureDestinationId,
            command.DefaultApplicationsDestinationId,
            command.DefaultSourceCodeDestinationId));
        if (configured.IsError)
        {
            return configured.Errors;
        }

        var priorUses = await dbContext.Query<GitConnectionPublicationUse>()
            .Where(usage => usage.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        var usedConnections = command.Destinations
            .Select(destination => new GitConnectionId(destination.GitConnectionId))
            .Distinct()
            .ToHashSet();
        var priorConnectionIds = priorUses.Select(usage => usage.ConnectionId).ToHashSet();
        dbContext.Query<GitConnectionPublicationUse>().RemoveRange(
            priorUses.Where(usage => !usedConnections.Contains(usage.ConnectionId)));
        foreach (var connectionId in usedConnections.Where(connectionId => !priorConnectionIds.Contains(connectionId)))
        {
            dbContext.Query<GitConnectionPublicationUse>().Add(new GitConnectionPublicationUse(
                project.OrganizationId,
                connectionId,
                project.Id));
        }

        return new ModelCommandOutcome($"Project {project.Id.Value:D} publication plan updated.", []);
    }
}
