namespace InfraFlowSculptor.Engine.Naming;

public sealed record NamingSanitizationResult(string Value, IReadOnlyList<string> Transformations);
