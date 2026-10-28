namespace InfraFlowSculptor.Engine.Model;

public sealed record ComponentSingleTargetSnapshot(
    Guid Id,
    string Code,
    Guid SubscriptionId,
    string Region,
    string DeploymentConnection,
    bool IsProtected,
    IReadOnlyList<string> Approvers,
    IReadOnlyList<string> Runners);
