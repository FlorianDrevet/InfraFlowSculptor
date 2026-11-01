namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ApplicationFindingResponse(string Code, string StepName, string? ParameterName, string Message);
