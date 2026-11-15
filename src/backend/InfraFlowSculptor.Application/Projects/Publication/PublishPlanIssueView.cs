namespace InfraFlowSculptor.Application.Projects.Publication;

public sealed record PublishPlanIssueView(
    string Code,
    string Message,
    Guid? ComponentId,
    string? Part,
    Guid? DestinationId);
