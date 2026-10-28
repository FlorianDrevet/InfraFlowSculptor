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

        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Apply(tags, snapshot.Tags);
        Apply(tags, target.Target.Tags);
        Apply(tags, component.Tags);
        Apply(tags, resourceGroup.Tags);
        Apply(tags, resource.Tags);

        if (snapshot.Project.SystemTagsEnabled)
        {
            tags[ProjectTagKey] = snapshot.Project.Code;
            tags[ComponentTagKey] = component.Code;
            tags[EnvironmentTagKey] = target.Target.Code;
            tags[ManagedByTagKey] = ManagedByValue;
        }

        return tags;
    }

    private static void Apply(Dictionary<string, string> target, IReadOnlyDictionary<string, string> source)
    {
        foreach (var tag in source)
        {
            target[tag.Key] = tag.Value;
        }
    }
}
