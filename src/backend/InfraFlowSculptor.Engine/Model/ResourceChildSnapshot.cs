using System.Text.Json;

namespace InfraFlowSculptor.Engine.Model;

public sealed record ResourceChildSnapshot(
    Guid Id,
    string Type,
    string Name,
    IReadOnlyDictionary<string, JsonElement> Properties);
