namespace InfraFlowSculptor.Engine.Plan;

public sealed record DeploymentPlan(
    ProjectInfo Project,
    IReadOnlyList<ComponentPlan> Components,
    CatalogPins Pins);
