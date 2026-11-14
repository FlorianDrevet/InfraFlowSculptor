namespace InfraFlowSculptor.Contracts.Projects;

public sealed record PublishPlanDestinationResponse(
    Guid Id,
    Guid GitConnectionId,
    string ConnectionName,
    string Provider,
    string ProviderRepositoryId,
    string RepositoryName,
    string DefaultBranch,
    string BasePath,
    bool IsAccessible);
