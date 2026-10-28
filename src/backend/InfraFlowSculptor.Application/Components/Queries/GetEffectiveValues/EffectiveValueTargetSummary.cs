namespace InfraFlowSculptor.Application.Components.Queries.GetEffectiveValues;

public sealed record EffectiveValueTargetSummary(
    Guid Id,
    string Code,
    bool IsPresent,
    string Region,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<EffectivePropertySummary> Properties);
