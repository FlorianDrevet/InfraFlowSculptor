namespace InfraFlowSculptor.Application.Components;

public sealed record ResourceGroupSummary(
    Guid Id,
    int Version,
    string Name,
    string? Region,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyDictionary<Guid, string> ForcedNames);
