namespace InfraFlowSculptor.Domain.ProjectAggregate;

public sealed record PublishPlanConfiguration(
    PublishPreset Preset,
    IReadOnlyCollection<PublishPlanDestinationInput> Destinations,
    IReadOnlyCollection<PublishPlanAssignmentInput> Assignments,
    Guid? DefaultInfrastructureDestinationId,
    Guid? DefaultApplicationsDestinationId,
    Guid? DefaultSourceCodeDestinationId);
