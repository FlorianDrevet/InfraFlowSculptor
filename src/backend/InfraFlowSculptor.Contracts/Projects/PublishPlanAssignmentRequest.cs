namespace InfraFlowSculptor.Contracts.Projects;

public sealed record PublishPlanAssignmentRequest(Guid ComponentId, string Part, Guid DestinationId);
