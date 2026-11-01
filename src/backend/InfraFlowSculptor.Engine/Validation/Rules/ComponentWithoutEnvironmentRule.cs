using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ComponentWithoutEnvironmentRule : IValidationRule
{
    public string Code => "VAL-CMP-SANS-ENVIRONNEMENT";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Snapshot.Components
        .Where(component => string.Equals(component.Mode, "PerEnvironment", StringComparison.Ordinal)
            && !component.TargetsAllEnvironments
            && component.TargetEnvironmentIds.Count == 0)
        .Select(component => context.Finding(
            Code,
            Severity,
            new ObjectRef(ObjectType.Component, component.Id),
            $"Le composant « {component.Code} » ne cible aucun environnement.",
            $"Component '{component.Code}' does not target any environment."))
        .ToArray();
}
