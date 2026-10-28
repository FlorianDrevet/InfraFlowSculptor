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
        component.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase),
        component.ResourceGroups.Select(group => new ResourceGroupSummary(
            group.Id,
            group.Version,
            group.Name,
            group.Region,
            group.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase),
            group.ForcedNames.ToDictionary(item => item.EnvironmentId, item => item.Name))).ToArray());
}
