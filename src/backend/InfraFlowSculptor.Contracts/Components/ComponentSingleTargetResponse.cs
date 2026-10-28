namespace InfraFlowSculptor.Contracts.Components;

public sealed record ComponentSingleTargetResponse(
    string Code,
    Guid SubscriptionId,
    string Region,
    string DeploymentConnection,
    bool IsProtected,
    IReadOnlyList<string> Approvers,
    IReadOnlyList<string> Runners);
