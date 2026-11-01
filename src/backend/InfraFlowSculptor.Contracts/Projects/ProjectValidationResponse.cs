namespace InfraFlowSculptor.Contracts.Projects;

public sealed record ProjectValidationResponse(
    Guid ProjectId,
    int ModelVersion,
    string CatalogVersion,
    int ErrorCount,
    int PublicationErrorCount,
    int WarningCount,
    int InformationCount,
    int AcknowledgedCount,
    ProjectValidationReadinessResponse Readiness,
    IReadOnlyList<ProjectValidationComponentResponse> OrderedComponents,
    IReadOnlyList<ProjectValidationFindingResponse> Findings);
