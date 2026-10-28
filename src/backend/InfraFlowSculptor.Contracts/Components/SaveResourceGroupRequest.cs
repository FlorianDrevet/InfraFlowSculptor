namespace InfraFlowSculptor.Contracts.Components;

public sealed record SaveResourceGroupRequest(
    string Name,
    string? Region,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyDictionary<Guid, string> ForcedNames,
    int ExpectedVersion);
