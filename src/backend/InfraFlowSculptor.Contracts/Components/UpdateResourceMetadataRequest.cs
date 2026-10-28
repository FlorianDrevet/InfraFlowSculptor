namespace InfraFlowSculptor.Contracts.Components;

public sealed record UpdateResourceMetadataRequest(
    string Description,
    IReadOnlyDictionary<string, string> Tags,
    int ExpectedVersion);
