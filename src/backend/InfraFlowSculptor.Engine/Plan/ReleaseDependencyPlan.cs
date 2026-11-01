namespace InfraFlowSculptor.Engine.Plan;

public sealed record ReleaseDependencyPlan(
    string Component,
    IReadOnlyList<ReleaseDependencyResourcePlan> Resources);
