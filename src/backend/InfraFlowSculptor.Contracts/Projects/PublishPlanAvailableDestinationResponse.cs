namespace InfraFlowSculptor.Contracts.Projects;

public sealed record PublishPlanAvailableDestinationResponse(
    Guid GitConnectionId,
    string ConnectionName,
    string Provider,
    string ProviderRepositoryId,
    string RepositoryName,
    string DefaultBranch);
