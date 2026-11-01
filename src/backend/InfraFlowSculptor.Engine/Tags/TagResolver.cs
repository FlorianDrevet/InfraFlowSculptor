using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Targets;

namespace InfraFlowSculptor.Engine.Tags;

public static class TagResolver
{
    private const string ProjectTagKey = "ifs-project";
    private const string ComponentTagKey = "ifs-component";
    private const string EnvironmentTagKey = "ifs-environment";
    private const string ManagedByTagKey = "managed-by";
    private const string ManagedByValue = "infraflowsculptor";

    public static IReadOnlyDictionary<string, string> Resolve(
        ModelSnapshot snapshot,
        ComponentSnapshot component,
        ResourceGroupSnapshot resourceGroup,
        ResourceSnapshot resource,
        ResolvedResourceTarget target)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(resourceGroup);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(target);

        var tags = CreateBase(snapshot, component, resourceGroup, target.Target.Tags);
        Apply(tags, resource.Tags);
        ApplySystemTags(tags, snapshot, component, target.Target.Code);

        return tags;
    }

    public static IReadOnlyDictionary<string, string> Resolve(
        ModelSnapshot snapshot,
        ComponentSnapshot component,
        ResourceGroupSnapshot resourceGroup,
        ResolvedResourceGroupTarget target)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(resourceGroup);
        ArgumentNullException.ThrowIfNull(target);

        var tags = CreateBase(snapshot, component, resourceGroup, target.Target.Tags);
        ApplySystemTags(tags, snapshot, component, target.Target.Code);
        return tags;
    }

    private static Dictionary<string, string> CreateBase(
        ModelSnapshot snapshot,
        ComponentSnapshot component,
        ResourceGroupSnapshot resourceGroup,
        IReadOnlyDictionary<string, string> targetTags)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Apply(tags, snapshot.Tags);
        Apply(tags, targetTags);
        Apply(tags, component.Tags);
        Apply(tags, resourceGroup.Tags);
        return tags;
    }

    private static void ApplySystemTags(
        Dictionary<string, string> tags,
        ModelSnapshot snapshot,
        ComponentSnapshot component,
        string targetCode)
    {
        if (!snapshot.Project.SystemTagsEnabled)
        {
            return;
        }

        tags[ProjectTagKey] = snapshot.Project.Code;
        tags[ComponentTagKey] = component.Code;
        tags[EnvironmentTagKey] = targetCode;
        tags[ManagedByTagKey] = ManagedByValue;
    }

    private static void Apply(Dictionary<string, string> target, IReadOnlyDictionary<string, string> source)
    {
        foreach (var tag in source)
        {
            target[tag.Key] = tag.Value;
        }
    }
}
