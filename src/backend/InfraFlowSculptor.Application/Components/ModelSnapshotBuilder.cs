using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Model;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components;

public sealed class ModelSnapshotBuilder(IIfsDbContext dbContext)
{
    public async Task<ErrorOr<ModelSnapshot>> BuildAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.Environments)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(projectId), cancellationToken);
        if (project is null)
        {
            return Error.NotFound("PROJECT_NOT_FOUND", "The project was not found.");
        }

        var components = await dbContext.Query<ComponentAggregate>()
            .Where(component => component.ProjectId == new ProjectId(projectId))
            .ToArrayAsync(cancellationToken);

        return new ModelSnapshot(
            new ProjectSnapshot(project.Name, project.Code, project.Description)
            {
                CatalogVersion = project.CatalogVersion,
                SystemTagsEnabled = project.SystemTagsEnabled
            },
            project.Environments.OrderBy(environment => environment.Position)
                .Select(environment => new EnvironmentSnapshot(environment.Code, environment.Region)
                {
                    Id = environment.Id,
                    Name = environment.Name,
                    Position = environment.Position,
                    SubscriptionId = environment.SubscriptionId,
                    Tags = environment.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase)
                })
                .ToArray(),
            new NamingConventionSnapshot(
                project.NamingPreset,
                project.DefaultNameTemplate,
                project.ResourceGroupNameTemplate),
            project.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase),
            components.Select(MapComponent).ToArray());
    }

    private static ComponentSnapshot MapComponent(ComponentAggregate component) => new(
        component.Id.Value,
        component.Name,
        component.Code,
        component.Mode.ToString(),
        component.TargetEnvironmentIds,
        component.SingleTarget is { } target
            ? new ComponentSingleTargetSnapshot(
                target.Id,
                target.Code,
                target.SubscriptionId,
                target.Region,
                target.DeploymentConnection,
                target.IsProtected,
                target.Approvers,
                target.Runners)
            : null,
        component.ResourceGroups.Select(group => new ResourceGroupSnapshot(
            group.Id,
            group.Name,
            group.Region,
            group.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase),
            group.ForcedNames.ToDictionary(value => value.EnvironmentId, value => value.Name))).ToArray(),
        component.Resources.Select(resource => new ResourceSnapshot(
            resource.Id,
            resource.ResourceGroupId,
            resource.Type,
            resource.LogicalName,
            resource.Description,
            resource.IsExisting,
            resource.GetProperties(),
            resource.GetOverrides(),
            resource.Presences.ToDictionary(value => value.TargetId, value => value.IsPresent),
            resource.ExistingIds.ToDictionary(value => value.TargetId, value => value.Value),
            resource.ForcedNames.ToDictionary(value => value.TargetId, value => value.Name),
            resource.Tags.ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase),
            resource.Children.Select(child => new ResourceChildSnapshot(
                child.Id,
                child.Type,
                child.Name,
                child.GetProperties())).ToArray())).ToArray())
    {
        TargetsAllEnvironments = component.TargetsAllEnvironments,
        Tags = component.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase)
    };
}
