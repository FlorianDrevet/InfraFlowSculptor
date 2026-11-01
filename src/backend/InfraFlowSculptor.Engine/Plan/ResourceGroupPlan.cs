namespace InfraFlowSculptor.Engine.Plan;

public sealed record ResourceGroupPlan(
    string LogicalName,
    string? Name,
    string Region,
    bool Present,
    IReadOnlyDictionary<string, string> Tags);
