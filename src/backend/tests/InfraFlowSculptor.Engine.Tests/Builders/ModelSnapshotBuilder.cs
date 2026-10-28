using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Tests.Builders;

public sealed class ModelSnapshotBuilder
{
    private ProjectSnapshot project = new("Boutique en ligne", "shop", string.Empty)
    {
        CatalogVersion = "2026.10-pilot"
    };

    private NamingConventionSnapshot naming = new(
        "Compact",
        "{abbr}-{project}-{name}-{env}",
        "rg-{project}-{component}-{name}-{env}");

    private readonly Dictionary<string, string> projectTags = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<EnvironmentSnapshot> environments = [];
    private readonly List<ComponentSnapshot> components = [];
    private bool? systemTagsEnabled;

    public ModelSnapshotBuilder WithProject(ProjectSnapshot value)
    {
        project = value;
        return this;
    }

    public ModelSnapshotBuilder WithNaming(NamingConventionSnapshot value)
    {
        naming = value;
        return this;
    }

    public ModelSnapshotBuilder WithProjectTags(params KeyValuePair<string, string>[] values)
    {
        projectTags.Clear();
        foreach (var value in values)
        {
            projectTags.Add(value.Key, value.Value);
        }

        return this;
    }

    public ModelSnapshotBuilder WithSystemTagsEnabled(bool enabled)
    {
        systemTagsEnabled = enabled;
        return this;
    }

    public ModelSnapshotBuilder WithEnvironment(EnvironmentSnapshot value)
    {
        environments.Add(value);
        return this;
    }

    public ModelSnapshotBuilder WithComponent(ComponentSnapshot value)
    {
        components.Add(value);
        return this;
    }

    public ModelSnapshot Build() => new(
        project with { SystemTagsEnabled = systemTagsEnabled ?? project.SystemTagsEnabled },
        environments.ToArray(),
        naming,
        new Dictionary<string, string>(projectTags, StringComparer.OrdinalIgnoreCase),
        components.ToArray());
}
