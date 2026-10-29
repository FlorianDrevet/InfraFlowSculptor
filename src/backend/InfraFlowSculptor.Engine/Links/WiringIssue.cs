namespace InfraFlowSculptor.Engine.Links;

public sealed record WiringIssue(string Code, string Message, Guid? LinkId, Guid? ResourceId);
