namespace InfraFlowSculptor.Contracts.Projects;

public sealed record PublishPlanComponentResponse(
    Guid Id,
    string Name,
    string Code,
    bool HasApplications,
    Guid? InfrastructureDestinationId,
    Guid? ApplicationsDestinationId,
    Guid? SourceCodeDestinationId);
