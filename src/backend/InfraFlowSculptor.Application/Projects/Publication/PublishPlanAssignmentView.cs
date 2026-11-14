namespace InfraFlowSculptor.Application.Projects.Publication;

public sealed record PublishPlanAssignmentView(Guid ComponentId, string Part, Guid DestinationId);
