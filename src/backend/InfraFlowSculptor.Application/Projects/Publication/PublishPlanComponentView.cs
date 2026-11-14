namespace InfraFlowSculptor.Application.Projects.Publication;

public sealed record PublishPlanComponentView(
    Guid Id,
    string Name,
    string Code,
    bool HasApplications,
    Guid? InfrastructureDestinationId,
    Guid? ApplicationsDestinationId,
    Guid? SourceCodeDestinationId);
