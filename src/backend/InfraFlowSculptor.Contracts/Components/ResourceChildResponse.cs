using System.Text.Json;

namespace InfraFlowSculptor.Contracts.Components;

public sealed record ResourceChildResponse(
    Guid Id,
    string Type,
    string Name,
    IReadOnlyDictionary<string, JsonElement> Properties);
