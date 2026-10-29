using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ComponentCycleRule : IValidationRule
{
    public string Code => "VAL-CMP-CYCLE";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Ordering.Cycles
        .Select(cycle =>
        {
            var components = cycle.ComponentIds
                .Select(id => context.Snapshot.Components.First(component => component.Id == id))
                .ToArray();
            var links = cycle.Dependencies.Select(dependency => dependency.LinkId).Distinct().Order().ToArray();
            var componentNames = string.Join(", ", components.Select(component => $"« {component.Code} »"));
            var linkNames = string.Join(", ", links.Select(id => id.ToString("D")));
            return context.Finding(
                Code,
                Severity,
                new ObjectRef(ObjectType.Component, components[0].Id),
                $"Les dépendances de création forment un cycle entre {componentNames} (liaisons : {linkNames}). Modifiez une liaison de création ou réorganisez le modèle.",
                $"Creation dependencies form a cycle between {string.Join(", ", components.Select(component => $"'{component.Code}'"))} (links: {linkNames}). Change a creation link or reorganize the model.",
                relatedLinkIds: links);
        })
        .ToArray();
}
