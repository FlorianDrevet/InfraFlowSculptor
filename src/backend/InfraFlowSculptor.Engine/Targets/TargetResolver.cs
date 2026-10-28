using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Targets;

public static class TargetResolver
{
    private const string PerEnvironmentMode = "PerEnvironment";
    private const string SingleMode = "Single";

    public static IReadOnlyList<ResolvedResourceTarget> ResolveResourceTargets(
        ModelSnapshot snapshot,
        ComponentSnapshot component,
        ResourceSnapshot resource)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(resource);

        var resourceGroup = component.ResourceGroups.SingleOrDefault(group => group.Id == resource.ResourceGroupId)
            ?? throw new ArgumentException("The resource group must belong to the component.", nameof(resource));

        return ResolveTargets(snapshot, component)
            .Select(target => new ResolvedResourceTarget(
                target,
                string.IsNullOrWhiteSpace(resourceGroup.Region) ? target.Region : resourceGroup.Region,
                ResolvePresence(resource, target.Id)))
            .ToArray();
    }

    public static IReadOnlyList<ResolvedResourceGroupTarget> ResolveResourceGroupTargets(
        ModelSnapshot snapshot,
        ComponentSnapshot component,
        ResourceGroupSnapshot resourceGroup)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(resourceGroup);

        return ResolveTargets(snapshot, component)
            .Select(target => new ResolvedResourceGroupTarget(
                target,
                string.IsNullOrWhiteSpace(resourceGroup.Region) ? target.Region : resourceGroup.Region,
                component.Resources.Any(resource => resource.ResourceGroupId == resourceGroup.Id
                    && ResolvePresence(resource, target.Id))))
            .ToArray();
    }

    public static IReadOnlyList<ResolvedTarget> ResolveTargets(ModelSnapshot snapshot, ComponentSnapshot component)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(component);

        if (string.Equals(component.Mode, SingleMode, StringComparison.Ordinal))
        {
            return component.SingleTarget is { } singleTarget
                ? [new ResolvedTarget(
                    singleTarget.Id,
                    singleTarget.Code,
                    singleTarget.SubscriptionId,
                    singleTarget.Region,
                    EmptyTags.Instance,
                    IsSingle: true)]
                : [];
        }

        if (!string.Equals(component.Mode, PerEnvironmentMode, StringComparison.Ordinal))
        {
            return [];
        }

        if (snapshot.Environments.Any(environment => environment.Id == Guid.Empty))
        {
            throw new ArgumentException("Every project environment must have a non-empty id.", nameof(snapshot));
        }

        var selectedEnvironmentIds = component.TargetEnvironmentIds.ToHashSet();
        return snapshot.Environments
            .Where(environment => component.TargetsAllEnvironments || selectedEnvironmentIds.Contains(environment.Id))
            .OrderBy(environment => environment.Position)
            .Select(environment => new ResolvedTarget(
                environment.Id,
                environment.Code,
                environment.SubscriptionId,
                environment.Region,
                environment.Tags,
                IsSingle: false))
            .ToArray();
    }

    private static bool ResolvePresence(ResourceSnapshot resource, Guid targetId)
    {
        var hasExistingId = resource.ExistingIds.ContainsKey(targetId);
        if (resource.IsExisting && !hasExistingId)
        {
            return false;
        }

        return resource.Presences.TryGetValue(targetId, out var isPresent)
            ? isPresent
            : hasExistingId || !resource.IsExisting;
    }

    private static class EmptyTags
    {
        public static readonly IReadOnlyDictionary<string, string> Instance =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
