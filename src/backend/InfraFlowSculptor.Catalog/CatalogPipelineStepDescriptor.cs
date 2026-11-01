using System.Collections.Immutable;
using System.Text.Json;

namespace InfraFlowSculptor.Catalog;

public sealed class CatalogPipelineStepDescriptor(JsonElement definition)
{
    public string Name { get; } = definition.GetProperty("name").GetString()!;

    public string LabelFr { get; } = definition.GetProperty("labels").GetProperty("fr").GetString()!;

    public string LabelEn { get; } = definition.GetProperty("labels").GetProperty("en").GetString()!;

    public string Description { get; } = definition.GetProperty("description").GetString()!;

    public bool DefaultEnabled { get; } = definition.GetProperty("defaults").GetProperty("enabled").GetBoolean();

    public bool DefaultBlocking { get; } = definition.GetProperty("defaults").GetProperty("blocking").GetBoolean();

    public string DefaultPosition { get; } = definition.GetProperty("defaults").GetProperty("position").GetString()!;

    public string Provider { get; } = definition.GetProperty("pipeline").GetProperty("provider").GetString()!;

    public string Template { get; } = definition.GetProperty("pipeline").GetProperty("template").GetString()!;

    public ImmutableArray<ParameterDescriptor> Parameters { get; } = definition.GetProperty("parameters")
        .EnumerateArray()
        .Select(parameter => new ParameterDescriptor(
            parameter.GetProperty("name").GetString()!,
            parameter.GetProperty("kind").GetString()!,
            parameter.TryGetProperty("default", out var defaultValue) ? defaultValue.ToString() : null,
            parameter.TryGetProperty("values", out var values)
                ? values.EnumerateArray().Select(value => value.GetString()!).ToImmutableArray()
                : []))
        .ToImmutableArray();

    public sealed record ParameterDescriptor(
        string Name,
        string Kind,
        string? DefaultValue,
        ImmutableArray<string> Values);
}
