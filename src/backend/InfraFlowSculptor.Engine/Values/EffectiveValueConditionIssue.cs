namespace InfraFlowSculptor.Engine.Values;

public sealed record EffectiveValueConditionIssue(
    string PropertyName,
    string DependencyName,
    bool HasNonDefaultValue);
