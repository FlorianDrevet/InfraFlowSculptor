using System.Text.Json;

namespace InfraFlowSculptor.Engine.Values;

public sealed record EffectivePropertyValue(
    string Name,
    JsonElement? Value,
    EffectiveValueProvenance Provenance);
