using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Targets;

namespace InfraFlowSculptor.Engine.Links;

public sealed class LinkPreviewAnalyzer(WiringEngine wiringEngine)
{
    public LinkPreviewResult Preview(ModelSnapshot snapshot, CatalogVersion catalog, LinkSnapshot candidate)
    {
        var before = wiringEngine.Evaluate(snapshot, catalog);
        var after = wiringEngine.Evaluate(snapshot with
        {
            Links = snapshot.Links.Append(candidate).ToArray()
        }, catalog);
        var resources = snapshot.Components.SelectMany(component => component.Resources
                .Select(resource => (Component: component, Resource: resource)))
            .ToDictionary(value => value.Resource.Id);
        var components = snapshot.Components.ToDictionary(component => component.Id);
        var source = resources[candidate.SourceResourceId];
        var target = resources[candidate.TargetResourceId];
        var targets = ResolveDeploymentTargets(snapshot, source, target);

        var existingRoleNames = before.RoleAssignments
            .Select(assignment => assignment.DeterministicName)
            .ToHashSet(StringComparer.Ordinal);
        var roleAssignments = after.RoleAssignments
            .Where(assignment => assignment.Origins.Any(origin => origin.LinkId == candidate.Id))
            .SelectMany(assignment => targets.Select(deploymentTarget => new LinkPreviewRoleAssignment(
                deploymentTarget.Id,
                deploymentTarget.Code,
                assignment.PrincipalResourceId,
                resources[assignment.PrincipalResourceId].Resource.LogicalName,
                assignment.RoleName,
                assignment.ScopeKind,
                assignment.ScopeResourceId,
                assignment.ScopeKind == LinkAccessScope.ResourceGroup
                    ? GetResourceGroupName(target.Component, assignment.ScopeResourceId)
                    : target.Resource.LogicalName,
                target.Resource.Id,
                target.Resource.LogicalName,
                existingRoleNames.Contains(assignment.DeterministicName))))
            .OrderBy(effect => effect.EnvironmentId)
            .ThenBy(effect => effect.RoleName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var identities = after.Identities
            .Where(identity => identity.Origins.Any(origin => origin.LinkId == candidate.Id))
            .SelectMany(identity => targets.Select(deploymentTarget => new LinkPreviewIdentity(
                deploymentTarget.Id,
                deploymentTarget.Code,
                identity.ResourceId,
                resources[identity.ResourceId].Resource.LogicalName,
                identity.Kind,
                identity.AttachedIdentityResourceId,
                identity.AttachedIdentityResourceId is { } identityResourceId
                    ? resources[identityResourceId].Resource.LogicalName
                    : null,
                before.Identities.Any(existing => SameIdentity(existing, identity)))))
            .OrderBy(effect => effect.EnvironmentId)
            .ThenBy(effect => effect.ResourceId)
            .ToArray();

        var dataAccess = after.DataAccessRequests
            .Where(request => request.Origin.LinkId == candidate.Id)
            .SelectMany(request => targets.Select(deploymentTarget => new LinkPreviewDataAccess(
                deploymentTarget.Id,
                deploymentTarget.Code,
                request.SourceResourceId,
                resources[request.SourceResourceId].Resource.LogicalName,
                request.TargetResourceId,
                resources[request.TargetResourceId].Resource.LogicalName,
                request.Level,
                request.DatabaseRoles)))
            .OrderBy(effect => effect.EnvironmentId)
            .ThenBy(effect => effect.TargetResourceId)
            .ToArray();

        var dependencies = after.Dependencies
            .Where(dependency => dependency.LinkId == candidate.Id)
            .SelectMany(dependency => targets.Select(deploymentTarget => new LinkPreviewDependency(
                deploymentTarget.Id,
                deploymentTarget.Code,
                dependency.SourceComponentId,
                components[dependency.SourceComponentId].Code,
                dependency.TargetComponentId,
                components[dependency.TargetComponentId].Code,
                dependency.LinkKind)))
            .OrderBy(effect => effect.EnvironmentId)
            .ThenBy(effect => effect.TargetComponentCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var previousSettingIds = before.ImplicitApplicationSettings.Select(setting => setting.Id).ToHashSet();
        var applicationSettings = after.ImplicitApplicationSettings
            .Where(setting => !previousSettingIds.Contains(setting.Id)
                && setting.ApplicationResourceId == candidate.SourceResourceId
                && setting.SourceResourceId == candidate.TargetResourceId)
            .SelectMany(setting => targets.Select(deploymentTarget => new LinkPreviewApplicationSetting(
                deploymentTarget.Id,
                deploymentTarget.Code,
                setting.ApplicationResourceId,
                resources[setting.ApplicationResourceId].Resource.LogicalName,
                setting.Name,
                setting.Origin,
                setting.IsSensitive)))
            .OrderBy(effect => effect.EnvironmentId)
            .ThenBy(effect => effect.Name, StringComparer.Ordinal)
            .ToArray();

        var findings = after.Issues
            .Where(issue => issue.LinkId == candidate.Id)
            .Select(issue => new LinkPreviewFinding(issue.Code, issue.Message, issue.ResourceId))
            .ToArray();

        return new LinkPreviewResult(roleAssignments, identities, dataAccess, dependencies, applicationSettings, findings);
    }

    private static ResolvedTarget[] ResolveDeploymentTargets(
        ModelSnapshot snapshot,
        (ComponentSnapshot Component, ResourceSnapshot Resource) source,
        (ComponentSnapshot Component, ResourceSnapshot Resource) target)
    {
        var sourceTargets = TargetResolver.ResolveResourceTargets(snapshot, source.Component, source.Resource);
        var targetTargets = TargetResolver.ResolveResourceTargets(snapshot, target.Component, target.Resource);
        return sourceTargets
            .Where(sourceTarget => sourceTarget.IsPresent && HasPresentTarget(sourceTarget.Target, targetTargets))
            .Select(sourceTarget => sourceTarget.Target)
            .DistinctBy(deploymentTarget => deploymentTarget.Id)
            .ToArray();
    }

    private static bool HasPresentTarget(ResolvedTarget sourceTarget, IReadOnlyList<ResolvedResourceTarget> targetTargets)
    {
        if (sourceTarget.IsSingle)
        {
            return targetTargets.Any(target => target.Target.IsSingle && target.IsPresent);
        }

        return targetTargets.Any(target => target.IsPresent
            && (target.Target.IsSingle || target.Target.Id == sourceTarget.Id));
    }

    private static bool SameIdentity(WiredIdentity left, WiredIdentity right) =>
        left.ComponentId == right.ComponentId
        && left.ResourceId == right.ResourceId
        && left.Kind == right.Kind
        && left.AttachedIdentityResourceId == right.AttachedIdentityResourceId;

    private static string GetResourceGroupName(ComponentSnapshot component, Guid resourceGroupId) =>
        component.ResourceGroups.Single(group => group.Id == resourceGroupId).Name;
}
