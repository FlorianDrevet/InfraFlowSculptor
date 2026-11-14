namespace InfraFlowSculptor.Contracts.Projects;

public sealed record ProjectPublishPlanResponse(
    Guid ProjectId,
    int ProjectVersion,
    string Preset,
    Guid? DefaultInfrastructureDestinationId,
    Guid? DefaultApplicationsDestinationId,
    Guid? DefaultSourceCodeDestinationId,
    IReadOnlyList<PublishPlanDestinationResponse> Destinations,
    IReadOnlyList<PublishPlanAssignmentResponse> Assignments,
    IReadOnlyList<PublishPlanComponentResponse> Components,
    IReadOnlyList<PublishPlanRowResponse> Rows,
    IReadOnlyList<PublishPlanAvailableDestinationResponse> AvailableDestinations,
    IReadOnlyList<PublishPlanIssueResponse> Issues);
