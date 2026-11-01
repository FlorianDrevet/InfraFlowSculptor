using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Targets;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class LinkPlacementRule : IValidationRule
{
    public string Code => "VAL-LIA-PLACEMENT";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var components = context.Snapshot.Components.ToDictionary(component => component.Id);
        var resources = context.Resources.ToDictionary(item => item.Resource.Id);
        var findings = new List<ValidationFinding>();
        foreach (var link in context.Snapshot.Links)
        {
            var handler = context.LinkKinds.Get(link.Kind);
            if (!handler.RequiresSameSubscriptionAndRegion
                || !components.TryGetValue(link.SourceComponentId, out var sourceComponent)
                || !components.TryGetValue(link.TargetComponentId, out var targetComponent)
                || !resources.TryGetValue(link.SourceResourceId, out var sourceResource)
                || !resources.TryGetValue(link.TargetResourceId, out var targetResource))
            {
                continue;
            }

            var sourceTargets = TargetResolver.ResolveResourceTargets(context.Snapshot, sourceComponent, sourceResource.Resource)
                .Where(target => target.IsPresent);
            var targetTargets = TargetResolver.ResolveResourceTargets(context.Snapshot, targetComponent, targetResource.Resource)
                .ToDictionary(target => target.Target.Id);
            foreach (var sourceTarget in sourceTargets)
            {
                var targetTarget = string.Equals(targetComponent.Mode, "Single", StringComparison.Ordinal)
                    ? targetTargets.Values.SingleOrDefault()
                    : targetTargets.GetValueOrDefault(sourceTarget.Target.Id);
                if (targetTarget is null || !targetTarget.IsPresent
                    || sourceTarget.Target.SubscriptionId == targetTarget.Target.SubscriptionId
                    && string.Equals(sourceTarget.Region, targetTarget.Region, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                findings.Add(context.Finding(
                    Code,
                    Severity,
                    new ObjectRef(ObjectType.Link, link.Id),
                    $"La source et la cible de la liaison d’hébergement ne partagent pas l’abonnement et la région de « {sourceTarget.Target.Code} ».",
                    $"Hosting source and target do not share the subscription and region for '{sourceTarget.Target.Code}'.",
                    sourceTarget.Target.Id,
                    relatedLinkIds: [link.Id]));
            }
        }

        return findings;
    }
}
