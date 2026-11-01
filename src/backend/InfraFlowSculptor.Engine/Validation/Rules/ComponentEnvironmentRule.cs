using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Targets;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ComponentEnvironmentRule : IValidationRule
{
    public string Code => "VAL-CMP-ENVIRONNEMENT";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var components = context.Snapshot.Components.ToDictionary(component => component.Id);
        var resources = context.Resources.ToDictionary(item => item.Resource.Id);
        var findings = new List<ValidationFinding>();
        foreach (var link in context.Snapshot.Links)
        {
            if (!components.TryGetValue(link.SourceComponentId, out var sourceComponent)
                || !components.TryGetValue(link.TargetComponentId, out var targetComponent)
                || !resources.TryGetValue(link.SourceResourceId, out var sourceResource)
                || !string.Equals(sourceComponent.Mode, "PerEnvironment", StringComparison.Ordinal)
                || !string.Equals(targetComponent.Mode, "PerEnvironment", StringComparison.Ordinal)
                || targetComponent.TargetsAllEnvironments)
            {
                continue;
            }

            foreach (var sourceTarget in TargetResolver.ResolveResourceTargets(context.Snapshot, sourceComponent, sourceResource.Resource)
                         .Where(target => target.IsPresent && !targetComponent.TargetEnvironmentIds.Contains(target.Target.Id)))
            {
                findings.Add(context.Finding(
                    Code,
                    Severity,
                    new ObjectRef(ObjectType.Link, link.Id),
                    $"Le composant cible « {targetComponent.Code} » ne cible pas l’environnement « {sourceTarget.Target.Code} » utilisé par la source.",
                    $"Target component '{targetComponent.Code}' does not target environment '{sourceTarget.Target.Code}' used by the source.",
                    sourceTarget.Target.Id,
                    relatedLinkIds: [link.Id]));
            }
        }

        return findings;
    }
}
