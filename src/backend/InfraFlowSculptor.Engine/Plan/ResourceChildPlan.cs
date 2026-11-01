namespace InfraFlowSculptor.Engine.Plan;

public sealed record ResourceChildPlan(
    string Type,
    string ArmType,
    string Name,
    IReadOnlyList<ResourcePropertyPlan> Properties);
