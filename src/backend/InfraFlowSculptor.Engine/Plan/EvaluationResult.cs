using InfraFlowSculptor.Engine.Naming;
using InfraFlowSculptor.Engine.Ordering;
using InfraFlowSculptor.Engine.Validation;

namespace InfraFlowSculptor.Engine.Plan;

public sealed record EvaluationResult(
    DeploymentPlan Plan,
    NamingPreviewResult Naming,
    ComponentOrderingResult Ordering,
    ValidationResult Validation);
