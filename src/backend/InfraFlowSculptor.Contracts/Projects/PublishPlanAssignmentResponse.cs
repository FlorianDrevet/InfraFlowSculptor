namespace InfraFlowSculptor.Contracts.Projects;

public sealed record PublishPlanAssignmentResponse(Guid ComponentId, string Part, Guid DestinationId);
