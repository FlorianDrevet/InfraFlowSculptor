namespace InfraFlowSculptor.Contracts.Projects;

public sealed record AddProjectEnvironmentRequest(
    string Name,
    string Code,
    int Position,
    Guid SubscriptionId,
    string Region,
    bool IsProtected,
    IReadOnlyList<string> Approvers,
    IReadOnlyList<string> Runners,
    string Description,
    IReadOnlyDictionary<string, string> Tags,
    int ExpectedVersion,
    string? DeploymentConnection = null);
