using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Application.Components;

internal static class ComponentSummaryMapper
{
    public static ComponentSummary Map(ComponentAggregate component) => new(
        component.Id.Value,
        component.ProjectId.Value,
        component.Version,
        component.Name,
        component.Code,
        component.Description,
        component.Mode,
        component.TargetsAllEnvironments,
        component.TargetEnvironmentIds,
        component.SingleTarget is { } target
            ? new ComponentSingleTargetSummary(
                target.Id,
                target.Code,
                target.SubscriptionId,
                target.Region,
                target.DeploymentConnection,
                target.IsProtected,
                target.Approvers,
                target.Runners)
            : null,
        component.RemovedResourceBehavior,
        component.DeletionProtectionEnabled,
        component.FirstPublishedAt,
        component.DefaultNameTemplate,
        component.NamingOverrides
            .OrderBy(value => value.Type, StringComparer.OrdinalIgnoreCase)
            .Select(value => new InfraFlowSculptor.Application.Naming.NamingOverrideValue(
                value.Type,
                value.Template,
                value.Abbreviation))
            .ToArray(),
        component.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase),
        component.ResourceGroups.Select(group => new ResourceGroupSummary(
            group.Id,
            group.Version,
            group.Name,
            group.Region,
            group.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase),
            group.ForcedNames.ToDictionary(item => item.EnvironmentId, item => item.Name))).ToArray(),
        component.Resources.Select(resource => new ResourceSummary(
            resource.Id,
            resource.Version,
            resource.ResourceGroupId,
            resource.Type,
            resource.LogicalName,
            resource.Description,
            resource.IsExisting,
            resource.GetProperties(),
            resource.GetOverrides(),
            resource.Presences.ToDictionary(item => item.TargetId, item => item.IsPresent),
            resource.ExistingIds.ToDictionary(item => item.TargetId, item => item.Value),
            resource.ForcedNames.ToDictionary(item => item.TargetId, item => item.Name),
            resource.Tags.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase),
            resource.Children.Select(child => new ResourceChildSummary(
                child.Id,
                child.Type,
                child.Name,
                child.GetProperties())).ToArray())).ToArray());
}
