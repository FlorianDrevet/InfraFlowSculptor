namespace InfraFlowSculptor.Contracts.Projects;

public sealed record PublishPlanIssueResponse(
    string Code,
    string Message,
    Guid? ComponentId,
    string? Part,
    Guid? DestinationId);
