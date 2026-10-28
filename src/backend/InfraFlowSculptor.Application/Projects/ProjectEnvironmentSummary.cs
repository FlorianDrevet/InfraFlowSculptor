namespace InfraFlowSculptor.Application.Projects;

public sealed record ProjectEnvironmentSummary(
    Guid Id,
    string Name,
    string Code,
    int Position,
    Guid SubscriptionId,
    string Region,
    string DeploymentConnection,
    bool IsProtected,
    IReadOnlyList<string> Approvers,
    IReadOnlyList<string> Runners,
    string Description,
    IReadOnlyDictionary<string, string> Tags,
    DateTimeOffset? FirstPublishedAt);
