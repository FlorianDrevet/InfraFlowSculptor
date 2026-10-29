using InfraFlowSculptor.Engine.Validation;

namespace InfraFlowSculptor.Application.Components.Queries.GetProjectValidation;

public sealed record ProjectValidationSummary(
    Guid ProjectId,
    int ModelVersion,
    string CatalogVersion,
    int ErrorCount,
    int PublicationErrorCount,
    int WarningCount,
    int InformationCount,
    int AcknowledgedCount,
    ValidationReadiness Readiness,
    IReadOnlyList<ProjectValidationComponentSummary> OrderedComponents,
    IReadOnlyList<ProjectValidationFindingSummary> Findings);
