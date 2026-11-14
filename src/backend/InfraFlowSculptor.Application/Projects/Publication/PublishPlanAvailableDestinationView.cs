namespace InfraFlowSculptor.Application.Projects.Publication;

public sealed record PublishPlanAvailableDestinationView(
    Guid GitConnectionId,
    string ConnectionName,
    string Provider,
    string ProviderRepositoryId,
    string RepositoryName,
    string DefaultBranch);
