using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Application.Naming;

public static class CatalogNamingSnapshotMapper
{
    public static IReadOnlyDictionary<string, NamingRuleSnapshot> CreateRules(CatalogVersion catalog)
    {
        var rules = new Dictionary<string, NamingRuleSnapshot>(StringComparer.OrdinalIgnoreCase)
        {
            [BuiltInNamingRules.ResourceGroupType] = BuiltInNamingRules.ResourceGroup
        };

        foreach (var descriptor in catalog.ResourceTypes)
        {
            rules[descriptor.Type] = MapRule(descriptor.Definition.GetProperty("naming"));
            foreach (var child in descriptor.Definition.GetProperty("children").EnumerateArray())
            {
                var childType = child.GetProperty("type").GetString()!;
                rules[$"{descriptor.Type}/{childType}"] = MapRule(child.GetProperty("naming"));
            }
        }

        return rules;
    }

    public static IReadOnlyDictionary<string, string> CreateRegionCodes(CatalogVersion catalog) =>
        catalog.Regions.ToDictionary(region => region.Name, region => region.Code, StringComparer.OrdinalIgnoreCase);

    private static NamingRuleSnapshot MapRule(JsonElement naming)
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
