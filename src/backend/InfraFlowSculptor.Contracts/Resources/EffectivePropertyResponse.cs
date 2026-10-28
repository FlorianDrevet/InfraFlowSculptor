using System.Text.Json;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record EffectivePropertyResponse(string Name, JsonElement? Value, string Provenance);
