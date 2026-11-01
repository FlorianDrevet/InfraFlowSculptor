namespace InfraFlowSculptor.Engine.Values;

public sealed record EffectiveValueResolution(
    IReadOnlyList<EffectivePropertyValue> Values,
    IReadOnlyList<EffectiveValueConditionIssue> UnresolvedConditions);
