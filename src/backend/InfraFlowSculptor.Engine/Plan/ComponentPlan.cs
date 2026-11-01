using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Engine.Plan;

public sealed record ComponentPlan(
    string ComponentCode,
    ComponentDeploymentMode Mode,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<TargetPlan> Targets,
    IReadOnlyList<ApplicationPlan> Applications);
