namespace InfraFlowSculptor.Engine.Targets;

public sealed record ResolvedResourceGroupTarget(ResolvedTarget Target, string Region, bool IsPresent);
