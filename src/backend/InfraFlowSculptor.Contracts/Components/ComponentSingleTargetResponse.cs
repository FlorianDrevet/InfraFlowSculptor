namespace InfraFlowSculptor.Contracts.Components;

public sealed record ComponentSingleTargetResponse(
    Guid Id,
    string Code,
    Guid SubscriptionId,
    string Region,
    string DeploymentConnection,
    bool IsProtected,
    IReadOnlyList<string> Approvers,
    IReadOnlyList<string> Runners);
