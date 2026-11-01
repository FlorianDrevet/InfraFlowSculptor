using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ComponentDirectionRule : IValidationRule
{
    public string Code => "VAL-CMP-SENS";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var components = context.Snapshot.Components.ToDictionary(component => component.Id);
        return context.Snapshot.Links
            .Where(link => components.TryGetValue(link.SourceComponentId, out var source)
                && components.TryGetValue(link.TargetComponentId, out var target)
                && string.Equals(source.Mode, "Single", StringComparison.Ordinal)
                && string.Equals(target.Mode, "PerEnvironment", StringComparison.Ordinal))
            .Select(link => context.Finding(
                Code,
                Severity,
                new ObjectRef(ObjectType.Link, link.Id),
                "Une ressource partagée ne peut pas dépendre d’une ressource déployée par environnement.",
                "A shared resource cannot depend on a resource deployed per environment.",
                relatedLinkIds: [link.Id]))
            .ToArray();
    }
}
