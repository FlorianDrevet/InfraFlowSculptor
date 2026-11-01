using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.DeleteComponent;

public sealed class DeleteComponentCommandHandler(IIfsDbContext dbContext)
    : ICommandHandler<DeleteComponentCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        DeleteComponentCommand command,
        CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(command.ProjectId);
        var component = await dbContext.Query<ComponentAggregate>()
            .Include(candidate => candidate.Resources)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ComponentId(command.ComponentId)
                && candidate.ProjectId == projectId,
                cancellationToken);
        if (component is null)
        {
            return Error.NotFound("COMPONENT_NOT_FOUND", "The component was not found.");
        }

        if (component.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The component changed. Reload it before deleting.",
                new Dictionary<string, object> { ["currentVersion"] = component.Version });
        }

        await dbContext.Query<ComponentAggregate>()
            .Where(candidate => candidate.Id == component.Id)
            .Include(candidate => candidate.ResourceGroups)
            .LoadAsync(cancellationToken);

        var canDelete = component.EnsureCanBeDeleted();
        if (canDelete.IsError)
        {
            return canDelete.Errors;
        }

        var resources = component.Resources.ToArray();
        var resourceIds = resources.Select(resource => resource.Id).ToArray();
        var affectedResources = await dbContext.Query<LinkAggregate>()
            .Where(link => link.ProjectId == projectId
                && link.SourceComponentId != command.ComponentId
                && (link.TargetComponentId == command.ComponentId
                    || (link.IdentityResourceId != null && resourceIds.Contains(link.IdentityResourceId.Value))))
            .Select(link => link.SourceResourceId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (affectedResources.Length > 0)
        {
            return Error.Validation(
                "COMPONENT_DELETE_BLOCKED",
                "Other components link to resources in this component or use its identity. Remove those links before deleting it.",
                new Dictionary<string, object>
                {
                    ["affectedResources"] = affectedResources.Select(resourceId => resourceId.ToString("D")).ToArray()
                });
        }

        var referencedApplicationSettings = await dbContext.Query<AppSettingAggregate>()
            .Where(setting => setting.ProjectId == projectId
                && setting.ComponentId != new ComponentId(command.ComponentId)
                && (setting.SourceResourceId != null && resourceIds.Contains(setting.SourceResourceId.Value)
                    || setting.KeyVaultResourceId != null && resourceIds.Contains(setting.KeyVaultResourceId.Value)
                    || setting.IdentityResourceId != null && resourceIds.Contains(setting.IdentityResourceId.Value)))
            .Select(setting => new { setting.Name, setting.ApplicationResourceId })
            .ToArrayAsync(cancellationToken);
        if (referencedApplicationSettings.Length > 0)
        {
            return Error.Validation(
                "COMPONENT_DELETE_BLOCKED",
                "Application settings in other components reference resources in this component. Update or delete those settings first.",
                new Dictionary<string, object>
                {
                    ["applicationSettings"] = referencedApplicationSettings.Select(setting => setting.Name).ToArray()
                });
        }

        foreach (var resource in resources)
        {
            var removed = component.RemoveResource(resource.Id);
            if (removed.IsError)
            {
                return removed.Errors;
            }
        }

        if (resources.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var links = await dbContext.Query<LinkAggregate>()
            .Where(link => link.ProjectId == projectId
                && (link.SourceComponentId == command.ComponentId
                    || link.TargetComponentId == command.ComponentId
                    || (link.IdentityResourceId != null && resourceIds.Contains(link.IdentityResourceId.Value))))
            .ToArrayAsync(cancellationToken);
        dbContext.Query<LinkAggregate>().RemoveRange(links);

        var otherComponentsUsingDefaultLogAnalytics = await dbContext.Query<ComponentAggregate>()
            .Where(candidate => candidate.ProjectId == projectId
                && candidate.Id != component.Id
                && candidate.DefaultLogAnalyticsResourceId != null
                && resourceIds.Contains(candidate.DefaultLogAnalyticsResourceId.Value))
            .ToArrayAsync(cancellationToken);
        foreach (var otherComponent in otherComponentsUsingDefaultLogAnalytics)
        {
            var cleared = otherComponent.SetDefaultLogAnalyticsResource(null);
            if (cleared.IsError)
            {
                return cleared.Errors;
            }
        }

        foreach (var resourceGroup in component.ResourceGroups.ToArray())
        {
            var removed = component.RemoveResourceGroup(resourceGroup.Id, containsResources: false);
            if (removed.IsError)
            {
                return removed.Errors;
            }
        }

        dbContext.Query<ComponentAggregate>().Remove(component);
        return new ModelCommandOutcome($"Component {component.Code} deleted.", []);
    }
}
