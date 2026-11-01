using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Engine.Plan;

public sealed record ApplicationStepPlan(
    string Name,
    bool Enabled,
    bool Blocking,
    ApplicationStepPosition Position,
    IReadOnlyList<ApplicationStepParameterPlan> Parameters);
