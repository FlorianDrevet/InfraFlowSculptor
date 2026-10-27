namespace InfraFlowSculptor.Application.Common.ModelCommands;

public sealed record ModelCommandOutcome(string Effect, IReadOnlyList<string> Findings);
