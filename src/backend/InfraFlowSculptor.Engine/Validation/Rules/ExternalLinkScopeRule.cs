using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ExternalLinkScopeRule : IValidationRule
{
    public string Code => "VAL-LIA-PORTEE-EXTERNE";

    public FindingSeverity Severity => FindingSeverity.Info;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var components = context.Snapshot.Components.ToDictionary(component => component.Id);
        var environments = context.Snapshot.Environments.ToDictionary(environment => environment.Id);
        var resources = context.Resources.ToDictionary(item => item.Resource.Id);
        var findings = new List<ValidationFinding>();

        foreach (var assignment in context.Ordering.Wiring.RoleAssignments.Where(value => value.IsExternal))
        {
            if (!components.TryGetValue(assignment.OwnerComponentId, out var owner)
                || !components.TryGetValue(assignment.TargetComponentId, out var target))
            {
                continue;
            }

            var isExternal = assignment.TargetResourceIds.Any(targetId =>
                resources.TryGetValue(targetId, out var resource) && resource.Resource.IsExisting)
                || HasDifferentSubscription(owner, target, environments);
            if (!isExternal)
            {
                continue;
            }

            if (assignment.Origins.Count == 0)
            {
                continue;
            }

            var origin = assignment.Origins[0];

            var links = assignment.Origins.Select(value => value.LinkId).Distinct().Order().ToArray();
            var targetName = assignment.TargetResourceIds
                .Select(id => resources.GetValueOrDefault(id).Resource?.LogicalName)
                .FirstOrDefault(name => name is not null) ?? assignment.RoleName;
            findings.Add(context.Finding(
                Code,
                Severity,
                new ObjectRef(ObjectType.Link, origin.LinkId),
                $"Le rôle « {assignment.RoleName} » sur « {targetName} » est attribué hors de l’abonnement du composant « {owner.Code} » ; prévoyez les droits de déploiement nécessaires.",
                $"Role '{assignment.RoleName}' on '{targetName}' is assigned outside the subscription of component '{owner.Code}'; provide the required deployment permissions.",
                relatedLinkIds: links));
        }

        return findings;
    }

    private static bool HasDifferentSubscription(
        ComponentSnapshot owner,
        ComponentSnapshot target,
        Dictionary<Guid, EnvironmentSnapshot> environments)
    {
        var ownerSubscriptions = GetSubscriptions(owner, environments);
        var targetSubscriptions = GetSubscriptions(target, environments);
        if (ownerSubscriptions.Count == 0 || targetSubscriptions.Count == 0)
        {
            return false;
        }

        var ownerEnvironmentIds = GetEnvironmentIds(owner, environments);
        var targetEnvironmentIds = GetEnvironmentIds(target, environments);
        var sharedEnvironmentIds = ownerEnvironmentIds.Intersect(targetEnvironmentIds).ToArray();
        if (sharedEnvironmentIds.Length > 0)
        {
            return sharedEnvironmentIds.Any(id => environments.TryGetValue(id, out var environment)
                && IsSingleTargetInDifferentSubscription(owner, target, id, environment.SubscriptionId, environments));
        }

        return ownerSubscriptions.Except(targetSubscriptions).Any()
            || targetSubscriptions.Except(ownerSubscriptions).Any();
    }

    private static bool IsSingleTargetInDifferentSubscription(
        ComponentSnapshot owner,
        ComponentSnapshot target,
        Guid environmentId,
        Guid environmentSubscriptionId,
        Dictionary<Guid, EnvironmentSnapshot> environments)
    {
        var ownerSubscription = owner.SingleTarget?.SubscriptionId
            ?? (GetEnvironmentIds(owner, environments).Contains(environmentId)
                ? environmentSubscriptionId
                : Guid.Empty);
        var targetSubscription = target.SingleTarget?.SubscriptionId
            ?? (GetEnvironmentIds(target, environments).Contains(environmentId)
                ? environmentSubscriptionId
                : Guid.Empty);
        return ownerSubscription != Guid.Empty && targetSubscription != Guid.Empty && ownerSubscription != targetSubscription;
    }

    private static HashSet<Guid> GetSubscriptions(
        ComponentSnapshot component,
        Dictionary<Guid, EnvironmentSnapshot> environments)
    {
        if (component.SingleTarget is { } singleTarget)
        {
            return [singleTarget.SubscriptionId];
        }

        return GetEnvironmentIds(component, environments)
            .Where(environments.ContainsKey)
            .Select(id => environments[id].SubscriptionId)
            .ToHashSet();
    }

    private static HashSet<Guid> GetEnvironmentIds(
        ComponentSnapshot component,
        Dictionary<Guid, EnvironmentSnapshot> environments) => component.TargetsAllEnvironments
            ? environments.Keys.ToHashSet()
            : component.TargetEnvironmentIds.Where(environments.ContainsKey).ToHashSet();
}
