using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Ordering;

namespace InfraFlowSculptor.Engine.Validation;

public sealed record ValidationResult(
    Guid ProjectId,
    int ModelVersion,
    string CatalogVersion,
    IReadOnlyList<ComponentSnapshot> OrderedComponents,
    IReadOnlyList<ValidationFinding> Findings,
    ValidationReadiness Readiness,
    ComponentOrderingResult Ordering);
