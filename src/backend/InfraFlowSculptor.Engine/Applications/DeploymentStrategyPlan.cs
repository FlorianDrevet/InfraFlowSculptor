namespace InfraFlowSculptor.Engine.Applications;

public sealed record DeploymentStrategyPlan(
    IReadOnlyList<string> InfrastructurePrerequisites,
    IReadOnlyList<DeploymentStageStepKind> StageSteps);
