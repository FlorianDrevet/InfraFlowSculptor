namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ApplicationStepRequest(
    string Name,
    bool Enabled,
    bool Blocking,
    string Position,
    IReadOnlyList<ApplicationStepParameterRequest> Parameters);
