namespace InfraFlowSculptor.Application.Projects.Publication;

public sealed record ProjectPublishPlanView(
    Guid ProjectId,
    int ProjectVersion,
    string Preset,
    Guid? DefaultInfrastructureDestinationId,
    Guid? DefaultApplicationsDestinationId,
    Guid? DefaultSourceCodeDestinationId,
    IReadOnlyList<PublishPlanDestinationView> Destinations,
    IReadOnlyList<PublishPlanAssignmentView> Assignments,
    IReadOnlyList<PublishPlanComponentView> Components,
    IReadOnlyList<PublishPlanRowView> Rows,
    IReadOnlyList<PublishPlanAvailableDestinationView> AvailableDestinations,
    IReadOnlyList<PublishPlanIssueView> Issues);
