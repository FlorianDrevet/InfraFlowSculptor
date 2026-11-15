namespace InfraFlowSculptor.Application.Projects.Publication;

public sealed record PublishPlanDestinationView(
    Guid Id,
    Guid GitConnectionId,
    string ConnectionName,
    string Provider,
    string ProviderRepositoryId,
    string RepositoryName,
    string DefaultBranch,
    string BasePath,
    bool IsAccessible);
