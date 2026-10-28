namespace InfraFlowSculptor.Contracts.Projects;

public sealed record UpdateProjectEnvironmentRequest(
    string Name,
    string Code,
    Guid SubscriptionId,
    string Region,
    bool IsProtected,
    IReadOnlyList<string> Approvers,
    IReadOnlyList<string> Runners,
    string Description,
    IReadOnlyDictionary<string, string> Tags,
    bool ConfirmTargetImpact,
    int ExpectedVersion,
    string? DeploymentConnection = null);
