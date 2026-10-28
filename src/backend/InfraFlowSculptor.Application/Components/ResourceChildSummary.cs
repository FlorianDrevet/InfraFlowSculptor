using System.Text.Json;

namespace InfraFlowSculptor.Application.Components;

public sealed record ResourceChildSummary(
    Guid Id,
    string Type,
    string Name,
    IReadOnlyDictionary<string, JsonElement> Properties);
