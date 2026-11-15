namespace InfraFlowSculptor.Domain.ProjectAggregate;

public sealed record PublishPlanAssignmentInput(
    Guid ComponentId,
    PublishPart Part,
    Guid DestinationId);
