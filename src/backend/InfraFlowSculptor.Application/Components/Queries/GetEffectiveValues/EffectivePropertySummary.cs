using System.Text.Json;

namespace InfraFlowSculptor.Application.Components.Queries.GetEffectiveValues;

public sealed record EffectivePropertySummary(string Name, JsonElement? Value, string Provenance);
