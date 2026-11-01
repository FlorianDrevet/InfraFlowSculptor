using InfraFlowSculptor.Engine.Applications;

namespace InfraFlowSculptor.Engine.Plan;

public sealed record ApplicationTargetPlan(
    string TargetCode,
    string? BuildRegistryComponent,
    string? BuildRegistryResource,
    string? BuildRegistryName,
    string? BuildRegistryTarget,
    bool PromotionRequired,
    IReadOnlyList<string> InfrastructurePrerequisites,
    IReadOnlyList<DeploymentStageStepKind> StageSteps);
