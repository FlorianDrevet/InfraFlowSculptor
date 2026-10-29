using InfraFlowSculptor.Engine.Naming;
using InfraFlowSculptor.Engine.Ordering;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Applications;

namespace InfraFlowSculptor.Engine.Validation;

public sealed class ValidationEngine(
    IEnumerable<IRuleSource> ruleSources,
    ComponentOrdering componentOrdering,
    LinkKindRegistry linkKinds,
    DeploymentStrategyRegistry deploymentStrategies)
{
    public ValidationResult Evaluate(
        InfraFlowSculptor.Engine.Model.ModelSnapshot snapshot,
        InfraFlowSculptor.Catalog.CatalogVersion catalog)
    {
        var ordering = componentOrdering.Order(snapshot, catalog);
        var context = new ValidationContext(
            snapshot,
            catalog,
            ordering,
            NamingEngine.Preview(snapshot),
            linkKinds,
            deploymentStrategies);
        var findings = ruleSources
            .OrderBy(source => source.Key, StringComparer.Ordinal)
            .SelectMany(source => source.GetRules())
            .SelectMany(rule => rule.Evaluate(context with { Lot = rule.Lot }))
            .OrderBy(finding => finding.Severity)
            .ThenBy(finding => finding.Code, StringComparer.Ordinal)
            .ThenBy(finding => finding.Subject.Type)
            .ThenBy(finding => finding.Subject.Id)
            .ThenBy(finding => finding.EnvironmentId)
            .ThenBy(finding => finding.DetailKey, StringComparer.Ordinal)
            .ToArray();
        var hasError = findings.Any(finding => finding.Severity == FindingSeverity.Error);
        var hasPublicationError = findings.Any(finding => finding.Severity == FindingSeverity.ErrorAtPublish);
        return new ValidationResult(
            snapshot.Project.Id,
            snapshot.Project.ModelVersion,
            catalog.Version,
            ordering.OrderedComponents,
            findings,
            new ValidationReadiness(!hasError, !hasError, !hasError && !hasPublicationError, false),
            ordering);
    }
}
