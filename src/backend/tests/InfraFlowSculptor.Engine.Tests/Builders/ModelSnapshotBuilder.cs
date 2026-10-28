using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Tests.Builders;

public sealed class ModelSnapshotBuilder
{
    private static readonly CatalogVersion PilotCatalog = CatalogLoader.Load().LatestPublished!;

    private ProjectSnapshot project = new("Boutique en ligne", "shop", string.Empty)
    {
        CatalogVersion = "2026.10-pilot"
    };

    private NamingConventionSnapshot naming = new(
        "Compact",
        "{abbr}-{project}-{name}-{env}",
        "rg-{project}-{component}-{name}-{env}");

    private readonly Dictionary<string, string> projectTags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NamingRuleSnapshot> namingRules = DefaultNamingRules();
    private readonly Dictionary<string, string> regionCodes = DefaultRegionCodes();
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

    public ModelSnapshotBuilder WithNamingRule(string type, NamingRuleSnapshot value)
    {
        namingRules[type] = value;
        return this;
    }

    public ModelSnapshotBuilder WithRegionCode(string region, string code)
    {
        regionCodes[region] = code;
        return this;
    }

    public ModelSnapshot Build() => new ModelSnapshot(
        project with { SystemTagsEnabled = systemTagsEnabled ?? project.SystemTagsEnabled },
        environments.ToArray(),
        naming,
        new Dictionary<string, string>(projectTags, StringComparer.OrdinalIgnoreCase),
        components.ToArray())
    {
        NamingRules = new Dictionary<string, NamingRuleSnapshot>(namingRules, StringComparer.OrdinalIgnoreCase),
        RegionCodes = new Dictionary<string, string>(regionCodes, StringComparer.OrdinalIgnoreCase)
    };

    private static Dictionary<string, NamingRuleSnapshot> DefaultNamingRules() =>
        BuildNamingRules(PilotCatalog);

    private static Dictionary<string, NamingRuleSnapshot> BuildNamingRules(CatalogVersion catalog)
    {
        var rules = new Dictionary<string, NamingRuleSnapshot>(StringComparer.OrdinalIgnoreCase)
        {
            [BuiltInNamingRules.ResourceGroupType] = BuiltInNamingRules.ResourceGroup
        };

        foreach (var descriptor in catalog.ResourceTypes)
        {
            rules[descriptor.Type] = ReadRule(descriptor.Definition.GetProperty("naming"));
            foreach (var child in descriptor.Definition.GetProperty("children").EnumerateArray())
            {
                rules[$"{descriptor.Type}/{child.GetProperty("type").GetString()}"] = ReadRule(child.GetProperty("naming"));
            }
        }

        return rules;
    }

    private static Dictionary<string, string> DefaultRegionCodes() =>
        PilotCatalog.Regions.ToDictionary(region => region.Name, region => region.Code, StringComparer.OrdinalIgnoreCase);

    private static NamingRuleSnapshot ReadRule(JsonElement naming)
    {
        var availability = naming.GetProperty("availabilityDomain");
        return new NamingRuleSnapshot(
            naming.GetProperty("abbreviation").GetString()!,
            naming.GetProperty("allowed").GetString()!,
            naming.GetProperty("case").GetString()!,
            naming.GetProperty("separators").EnumerateArray().Select(separator => separator.GetString()!).ToArray(),
            naming.GetProperty("forbidConsecutiveSeparators").GetBoolean(),
            naming.GetProperty("minLength").GetInt32(),
            naming.GetProperty("maxLength").GetInt32(),
            naming.GetProperty("firstChar").GetString()!,
            naming.GetProperty("lastChar").GetString()!,
            naming.GetProperty("uniqueness").GetString()!,
            availability.ValueKind == JsonValueKind.Null ? null : availability.GetString());
    }
}
