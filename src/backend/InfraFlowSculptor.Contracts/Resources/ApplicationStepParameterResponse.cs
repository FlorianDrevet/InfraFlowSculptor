namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ApplicationStepParameterResponse(
    string Name,
    string Kind,
    string Value,
    string? DefaultValue,
    IReadOnlyList<string> Values);
