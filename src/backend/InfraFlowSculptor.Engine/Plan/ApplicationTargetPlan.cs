using InfraFlowSculptor.Engine.Applications;

namespace InfraFlowSculptor.Engine.Plan;

public sealed record ApplicationTargetPlan(
    string TargetCode,
    string? BuildRegistryComponent,
    string? BuildRegistryResource,
    string? BuildRegistryName,
    bool PromotionRequired,
    IReadOnlyList<string> InfrastructurePrerequisites,
    IReadOnlyList<DeploymentStageStepKind> StageSteps);
