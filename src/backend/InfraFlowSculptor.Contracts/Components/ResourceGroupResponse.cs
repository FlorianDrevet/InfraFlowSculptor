namespace InfraFlowSculptor.Contracts.Components;

public sealed record ResourceGroupResponse(
    Guid Id,
    int Version,
    string Name,
    string? Region,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyDictionary<Guid, string> ForcedNames);
