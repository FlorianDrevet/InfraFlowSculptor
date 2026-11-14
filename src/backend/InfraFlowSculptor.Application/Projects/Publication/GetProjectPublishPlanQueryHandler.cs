using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Publication;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Publication;

public sealed class GetProjectPublishPlanQueryHandler(IIfsDbContext dbContext)
    : IQueryHandler<GetProjectPublishPlanQuery, ErrorOr<ProjectPublishPlanView>>
{
    public async ValueTask<ErrorOr<ProjectPublishPlanView>> Handle(
        GetProjectPublishPlanQuery query,
        CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(query.ProjectId);
        var project = await dbContext.Query<ProjectAggregate>()
            .AsNoTracking()
            .Include(candidate => candidate.PublicationPlan.Destinations)
            .Include(candidate => candidate.PublicationPlan.Assignments)
            .SingleOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var components = await dbContext.Query<ComponentAggregate>()
            .AsNoTracking()
            .Where(component => component.ProjectId == projectId)
            .OrderBy(component => component.NameKey)
            .ToArrayAsync(cancellationToken);
        var componentIds = components.Select(component => component.Id).ToArray();
        var applications = await dbContext.Query<ApplicationAggregate>()
            .AsNoTracking()
            .Where(application => componentIds.Contains(application.ComponentId))
            .ToArrayAsync(cancellationToken);
        var hasApplications = applications.Select(application => application.ComponentId).ToHashSet();
        var sourcePathsByComponent = applications
            .GroupBy(application => application.ComponentId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(application => application.CodeSourcePath)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .FirstOrDefault() ?? string.Empty);

        var connections = await dbContext.Query<GitConnectionAggregate>()
            .AsNoTracking()
            .Include(connection => connection.Repositories)
            .ThenInclude(repository => repository.AllowedProjects)
            .ToArrayAsync(cancellationToken);
        var connectionsById = connections.ToDictionary(connection => connection.Id);
        var available = connections
            .SelectMany(connection => connection.Repositories
                .Where(repository => repository.CanAccess(projectId))
                .Select(repository => new PublishPlanAvailableDestinationView(
                    connection.Id.Value,
                    connection.Name,
                    connection.ProviderKind.ToString(),
                    repository.ProviderRepositoryId,
                    repository.Name,
                    repository.DefaultBranch)))
            .OrderBy(destination => destination.ConnectionName, StringComparer.Ordinal)
            .ThenBy(destination => destination.RepositoryName, StringComparer.Ordinal)
            .ToArray();

        var selectedDestinations = project.PublicationPlan.Destinations.Select(destination =>
        {
            var connectionFound = connectionsById.TryGetValue(destination.GitConnectionId, out var connection);
            var repository = connection?.Repositories.SingleOrDefault(candidate =>
                string.Equals(candidate.ProviderRepositoryId, destination.ProviderRepositoryId, StringComparison.OrdinalIgnoreCase));
            return new PublishPlanDestinationView(
                destination.Id,
                destination.GitConnectionId.Value,
                connection?.Name ?? string.Empty,
                connection?.ProviderKind.ToString() ?? string.Empty,
                destination.ProviderRepositoryId,
                repository?.Name ?? destination.ProviderRepositoryId,
                repository?.DefaultBranch ?? string.Empty,
                destination.BasePath,
                connectionFound && repository?.CanAccess(projectId) == true);
        }).ToArray();

        var providerByDestinationId = project.PublicationPlan.Destinations
            .Where(destination => connectionsById.ContainsKey(destination.GitConnectionId))
            .ToDictionary(
                destination => destination.Id,
                destination => connectionsById[destination.GitConnectionId].ProviderKind);
        var componentsForLayout = components.Select(component => new PublishComponent(
            component.Id.Value,
            component.Code,
            hasApplications.Contains(component.Id))).ToArray();
        var layout = PublishLayout.Build(
            project.PublicationPlan,
            project.CiPlatform,
            componentsForLayout,
            [],
            providerByDestinationId);
        var issues = layout.Issues.Select(issue => new PublishPlanIssueView(
            issue.Code,
            issue.Message,
            issue.ComponentId,
            issue.Part?.ToString(),
            issue.DestinationId))
            .Concat(selectedDestinations
                .Where(destination => !destination.IsAccessible)
                .Select(destination => new PublishPlanIssueView(
                    PublishValidationCodes.Destination,
                    "Le dépôt configuré n’existe plus ou n’est plus accessible à ce projet.",
                    null,
                    null,
                    destination.Id)))
            .ToArray();

        var componentViews = components.Select(component =>
        {
            var componentId = component.Id.Value;
            var includesApplications = hasApplications.Contains(component.Id);
            return new PublishPlanComponentView(
                componentId,
                component.Name,
                component.Code,
                includesApplications,
                project.PublicationPlan.ResolveDestination(componentId, PublishPart.Infrastructure),
                includesApplications ? project.PublicationPlan.ResolveDestination(componentId, PublishPart.Applications) : null,
                includesApplications ? project.PublicationPlan.ResolveDestination(componentId, PublishPart.SourceCode) : null);
        }).ToArray();

        var selectedById = selectedDestinations.ToDictionary(destination => destination.Id);
        var rows = components.SelectMany(component =>
        {
            var componentId = component.Id.Value;
            var includesApplications = hasApplications.Contains(component.Id);
            var parts = includesApplications
                ? new[] { PublishPart.Infrastructure, PublishPart.Applications, PublishPart.SourceCode }
                : [PublishPart.Infrastructure];
            return parts.Select(part =>
            {
                var destinationId = project.PublicationPlan.ResolveDestination(componentId, part);
                var destination = destinationId is { } id && selectedById.TryGetValue(id, out var selected)
                    ? selected
                    : null;
                var folder = destinationId is null
                    ? string.Empty
                    : PublishLayout.EffectiveFolder(
                        component.Code,
                        part,
                        part == PublishPart.SourceCode ? string.Empty : destination?.BasePath ?? string.Empty,
                        part == PublishPart.SourceCode ? sourcePathsByComponent.GetValueOrDefault(component.Id) : null);
                return new PublishPlanRowView(
                    componentId,
                    component.Name,
                    component.Code,
                    part.ToString(),
                    destinationId,
                    destination is null
                        ? string.Empty
                        : string.Concat(destination.ConnectionName, "/", destination.RepositoryName),
                    part == PublishPart.SourceCode ? string.Empty : destination?.BasePath ?? string.Empty,
                    folder);
            });
        }).ToArray();

        return new ProjectPublishPlanView(
            project.Id.Value,
            project.Version,
            project.PublicationPlan.Preset.ToString(),
            project.PublicationPlan.DefaultInfrastructureDestinationId,
            project.PublicationPlan.DefaultApplicationsDestinationId,
            project.PublicationPlan.DefaultSourceCodeDestinationId,
            selectedDestinations,
            project.PublicationPlan.Assignments.Select(assignment => new PublishPlanAssignmentView(
                assignment.ComponentId,
                assignment.Part.ToString(),
                assignment.DestinationId)).ToArray(),
            componentViews,
            rows,
            available,
            issues);
    }
}
