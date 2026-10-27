namespace InfraFlowSculptor.Application.Common.ModelCommands;

public sealed record ModelCommandExecution(
    ModelCommandMode Mode,
    IReadOnlyList<string> Effects,
    IReadOnlyList<string> Findings,
    string Summary);
