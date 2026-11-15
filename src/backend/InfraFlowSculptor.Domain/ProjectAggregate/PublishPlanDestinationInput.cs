namespace InfraFlowSculptor.Domain.ProjectAggregate;

public sealed record PublishPlanDestinationInput(
    Guid Id,
    Guid GitConnectionId,
    string ProviderRepositoryId,
    string BasePath);
