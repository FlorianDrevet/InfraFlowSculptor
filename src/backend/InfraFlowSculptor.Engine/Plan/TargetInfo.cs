namespace InfraFlowSculptor.Engine.Plan;

public sealed record TargetInfo(
    string Code,
    Guid SubscriptionId,
    string Region,
    string DeploymentConnection,
    bool IsProtected,
    IReadOnlyList<string> Approvers,
    IReadOnlyList<string> Executors);
