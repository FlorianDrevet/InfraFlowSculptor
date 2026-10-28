namespace InfraFlowSculptor.Application.Components;

public sealed record ComponentSingleTargetSummary(
    string Code,
    Guid SubscriptionId,
    string Region,
    string DeploymentConnection,
    bool IsProtected,
    IReadOnlyList<string> Approvers,
    IReadOnlyList<string> Runners);
