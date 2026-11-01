using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Targets;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ResourcePresenceRule : IValidationRule
{
    public string Code => "VAL-RES-PRESENCE";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var resources = context.Resources.ToDictionary(item => item.Resource.Id);
        var components = context.Snapshot.Components.ToDictionary(component => component.Id);
        var findings = new List<ValidationFinding>();
        foreach (var link in context.Snapshot.Links)
        {
            if (!resources.TryGetValue(link.SourceResourceId, out var source)
                || !resources.TryGetValue(link.TargetResourceId, out var target)
                || !components.TryGetValue(link.SourceComponentId, out var sourceComponent)
                || !components.TryGetValue(link.TargetComponentId, out var targetComponent)
                || !string.Equals(sourceComponent.Mode, "PerEnvironment", StringComparison.Ordinal)
                || !string.Equals(targetComponent.Mode, "PerEnvironment", StringComparison.Ordinal))
            {
                continue;
            }

            var presentTargetIds = TargetResolver.ResolveResourceTargets(context.Snapshot, sourceComponent, source.Resource)
                .Where(targetValue => targetValue.IsPresent)
                .Select(targetValue => targetValue.Target.Id)
                .ToHashSet();
            foreach (var targetId in presentTargetIds)
            {
                var targetValue = TargetResolver.ResolveResourceTargets(context.Snapshot, targetComponent, target.Resource)
                    .FirstOrDefault(value => value.Target.Id == targetId);
                if (targetValue is null || targetValue.IsPresent)
                {
                    continue;
                }

                var environment = context.Snapshot.Environments.FirstOrDefault(value => value.Id == targetId);
                findings.Add(context.Finding(
                    Code,
                    Severity,
                    new ObjectRef(ObjectType.Resource, target.Resource.Id),
                    $"« {source.Resource.LogicalName} » est présent dans « {environment?.Code ?? targetValue.Target.Code} », mais sa cible « {target.Resource.LogicalName} » est absente.",
                    $"'{source.Resource.LogicalName}' is present in '{environment?.Code ?? targetValue.Target.Code}', but its target '{target.Resource.LogicalName}' is absent.",
                    targetId,
                    fixCode: Code,
                    relatedLinkIds: [link.Id]));
            }
        }

        return findings;
    }
}
