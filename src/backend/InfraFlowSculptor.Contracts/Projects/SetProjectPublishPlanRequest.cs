namespace InfraFlowSculptor.Contracts.Projects;

public sealed record SetProjectPublishPlanRequest(
    string Preset,
    IReadOnlyList<PublishPlanDestinationRequest> Destinations,
    IReadOnlyList<PublishPlanAssignmentRequest> Assignments,
    Guid? DefaultInfrastructureDestinationId,
    Guid? DefaultApplicationsDestinationId,
    Guid? DefaultSourceCodeDestinationId,
    int ExpectedVersion);
