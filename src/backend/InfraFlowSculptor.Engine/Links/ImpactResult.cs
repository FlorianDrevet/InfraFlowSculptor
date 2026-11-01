namespace InfraFlowSculptor.Engine.Links;

public sealed record ImpactResult(ImpactSeverity Severity, IReadOnlyList<ImpactItem> Items);
