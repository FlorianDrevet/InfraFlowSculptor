namespace InfraFlowSculptor.Engine.Targets;

public sealed record ResolvedResourceTarget(ResolvedTarget Target, string Region, bool IsPresent);
