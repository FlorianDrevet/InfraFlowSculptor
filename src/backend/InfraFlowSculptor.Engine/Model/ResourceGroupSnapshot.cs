namespace InfraFlowSculptor.Engine.Model;

public sealed record ResourceGroupSnapshot(
    Guid Id,
    string Name,
    string? Region,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyDictionary<Guid, string> ForcedNames);
