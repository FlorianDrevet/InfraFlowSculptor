using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class EmptyComponentRule : IValidationRule
{
    public string Code => "VAL-CMP-VIDE";

    public FindingSeverity Severity => FindingSeverity.Info;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Snapshot.Components
        .Where(component => component.Resources.Count == 0)
        .Select(component => context.Finding(
            Code,
            Severity,
            new ObjectRef(ObjectType.Component, component.Id),
            $"Le composant « {component.Code} » ne contient aucune ressource.",
            $"Component '{component.Code}' contains no resources."))
        .ToArray();
}
