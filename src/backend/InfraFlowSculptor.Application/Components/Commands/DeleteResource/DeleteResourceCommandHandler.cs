using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.DeleteResource;

public sealed class DeleteResourceCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider,
    ImpactAnalyzer impactAnalyzer)
    : ICommandHandler<DeleteResourceCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        DeleteResourceCommand command,
        CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(
            dbContext, currentOrganization, command.ProjectId, command.ComponentId, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var component = loaded.Value.Component;
        var resource = component.Resources.SingleOrDefault(candidate => candidate.Id == command.ResourceId);
        if (resource is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        var version = ResourceCommandSupport.CheckResourceVersion(resource, command.ExpectedVersion);
        if (version.IsError)
        {
            return version.Errors;
        }

        var built = await snapshotBuilder.BuildAsync(command.ProjectId, cancellationToken);
        if (built.IsError)
        {
            return built.Errors;
        }

        var catalog = catalogProvider.GetVersion(built.Value.Project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var impact = impactAnalyzer.AnalyzeResourceDeletion(built.Value, resource.Id, catalog);
        if (impact.Severity == ImpactSeverity.Blocker)
        {
            return Error.Validation("RESOURCE_DELETE_BLOCKED", "The resource cannot be deleted while it would break a required link.");
        }

        var links = await dbContext.Query<LinkAggregate>()
            .Where(link => link.ProjectId == new ProjectId(command.ProjectId)
                && (link.SourceResourceId == resource.Id || link.TargetResourceId == resource.Id))
            .ToArrayAsync(cancellationToken);
        dbContext.Query<LinkAggregate>().RemoveRange(links);

        var componentsUsingDefaultLogAnalytics = await dbContext.Query<ComponentAggregate>()
            .Where(candidate => candidate.ProjectId == new ProjectId(command.ProjectId)
                && candidate.DefaultLogAnalyticsResourceId == resource.Id)
            .ToArrayAsync(cancellationToken);
        foreach (var componentUsingWorkspace in componentsUsingDefaultLogAnalytics)
        {
            var cleared = componentUsingWorkspace.SetDefaultLogAnalyticsResource(null);
            if (cleared.IsError)
            {
                return cleared.Errors;
            }
        }

        var removed = component.RemoveResource(resource.Id);
        return removed.IsError
            ? removed.Errors
            : new ModelCommandOutcome(
                $"Resource {resource.LogicalName} deleted.",
                impact.Items.Select(item => item.Message).ToArray());
    }
}
