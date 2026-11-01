namespace InfraFlowSculptor.Engine.Links;

public sealed record ImpactItem(string Code, ImpactSeverity Severity, string Message, Guid? LinkId, Guid? ResourceId);
