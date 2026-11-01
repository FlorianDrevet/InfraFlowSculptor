namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ApplicationStepResponse(
    string Name,
    string Label,
    string Description,
    bool Enabled,
    bool Blocking,
    string Position,
    IReadOnlyList<ApplicationStepParameterResponse> Parameters);
