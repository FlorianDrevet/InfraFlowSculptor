using System.Collections.Immutable;
using System.Text.Json;

namespace InfraFlowSculptor.Catalog;

public sealed class CatalogBuildProfileDescriptor(JsonElement definition)
{
    public string Name { get; } = definition.GetProperty("name").GetString()!;

    public string LabelFr { get; } = definition.GetProperty("labels").GetProperty("fr").GetString()!;

    public string LabelEn { get; } = definition.GetProperty("labels").GetProperty("en").GetString()!;

    public ImmutableArray<string> ResourceTypes { get; } = definition.GetProperty("resourceTypes")
        .EnumerateArray()
        .Select(value => value.GetString()!)
        .ToImmutableArray();

    public ImmutableArray<string> Steps { get; } = definition.GetProperty("steps")
        .EnumerateArray()
        .Select(value => value.GetString()!)
        .ToImmutableArray();
}
