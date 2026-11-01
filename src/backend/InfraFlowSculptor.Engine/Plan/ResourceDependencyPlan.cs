namespace InfraFlowSculptor.Engine.Plan;

public sealed record ResourceDependencyPlan(
    string Kind,
    string ComponentCode,
    string TargetCode,
    string ResourceType,
    string LogicalName,
    string? Name,
    string? ResourceGroupName,
    Guid SubscriptionId,
    string? ResourceId,
    bool IsExisting,
    bool IsExternal,
    bool CreatesDeploymentDependency);
