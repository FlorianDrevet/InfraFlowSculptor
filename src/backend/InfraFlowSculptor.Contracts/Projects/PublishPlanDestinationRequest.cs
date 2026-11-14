namespace InfraFlowSculptor.Contracts.Projects;

public sealed record PublishPlanDestinationRequest(
    Guid Id,
    Guid GitConnectionId,
    string ProviderRepositoryId,
    string BasePath);
