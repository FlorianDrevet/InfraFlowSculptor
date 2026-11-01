using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;
using InfraFlowSculptor.Engine.Ordering;
using InfraFlowSculptor.Engine.Validation;

namespace InfraFlowSculptor.Engine.Plan;

public sealed class EngineFacade(
    LinkKindRegistry linkKinds,
    WiringEngine wiringEngine,
    ComponentOrdering componentOrdering,
    ValidationEngine validationEngine,
    DeploymentPlanBuilder deploymentPlanBuilder)
{
    public EvaluationResult Evaluate(
        ModelSnapshot snapshot,
        CatalogVersion catalog,
        EvaluationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(linkKinds);
        ArgumentNullException.ThrowIfNull(wiringEngine);
        ArgumentNullException.ThrowIfNull(componentOrdering);
        ArgumentNullException.ThrowIfNull(validationEngine);
        ArgumentNullException.ThrowIfNull(deploymentPlanBuilder);
        _ = options;

        var naming = NamingEngine.Preview(snapshot, linkKinds);
        var wiring = wiringEngine.Evaluate(snapshot, catalog);
        var ordering = ComponentOrdering.Order(snapshot, catalog, wiring);
        var validation = validationEngine.Evaluate(snapshot, catalog, ordering, naming);
        var plan = deploymentPlanBuilder.Build(snapshot, catalog, ordering, naming);
        return new EvaluationResult(plan, naming, ordering, validation);
    }
}
