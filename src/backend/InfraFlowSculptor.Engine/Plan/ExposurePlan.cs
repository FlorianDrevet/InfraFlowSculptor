namespace InfraFlowSculptor.Engine.Plan;

public sealed record ExposurePlan(string Mode, IReadOnlyList<string> AllowedIpRanges);
