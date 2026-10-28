namespace InfraFlowSculptor.Contracts.Resources;

public sealed record EffectiveValueTargetResponse(
    Guid Id,
    string Code,
    bool IsPresent,
    string Region,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<EffectivePropertyResponse> Properties);
