namespace InfraFlowSculptor.Engine.Plan;

public sealed record ResourceIdentityPlan(
    string Kind,
    string ResourceName,
    string? ResourceId,
    bool Attached);
