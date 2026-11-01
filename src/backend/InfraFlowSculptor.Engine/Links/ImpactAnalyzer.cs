using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Links;

public sealed class ImpactAnalyzer(WiringEngine wiringEngine)
{
    private const string RoleRevocationMessage = "Accès révoqué dans Azure au prochain déploiement, y compris en production.";

    public ImpactResult AnalyzeLinkRemoval(ModelSnapshot snapshot, Guid linkId, CatalogVersion catalog)
    {
        var existing = snapshot.Links.SingleOrDefault(link => link.Id == linkId);
        if (existing is null)
        {
            return new ImpactResult(ImpactSeverity.Information, []);
        }

        var after = snapshot with { Links = snapshot.Links.Where(link => link.Id != linkId).ToArray() };
        return AnalyzeChange(snapshot, after, catalog, linkId, existing.SourceResourceId);
    }

    public ImpactResult AnalyzeLinkUpdate(ModelSnapshot snapshot, LinkSnapshot replacement, CatalogVersion catalog)
    {
        var existing = snapshot.Links.SingleOrDefault(link => link.Id == replacement.Id);
        if (existing is null)
        {
            return new ImpactResult(ImpactSeverity.Information, []);
        }

        var after = snapshot with
        {
            Links = snapshot.Links.Select(link => link.Id == replacement.Id ? replacement : link).ToArray()
        };
        return AnalyzeChange(snapshot, after, catalog, replacement.Id, existing.SourceResourceId);
    }

    public ImpactResult AnalyzeResourceDeletion(ModelSnapshot snapshot, Guid resourceId, CatalogVersion catalog)
    {
        var component = snapshot.Components.SingleOrDefault(candidate => candidate.Resources.Any(resource => resource.Id == resourceId));
        if (component is null)
        {
            return new ImpactResult(ImpactSeverity.Information, []);
        }

        var after = snapshot with
        {
            Components = snapshot.Components.Select(candidate => candidate with
                {
                    Resources = candidate.Id == component.Id
                        ? candidate.Resources.Where(resource => resource.Id != resourceId).ToArray()
                        : candidate.Resources,
                    DefaultLogAnalyticsResourceId = candidate.DefaultLogAnalyticsResourceId == resourceId
                        ? null
                        : candidate.DefaultLogAnalyticsResourceId
                }).ToArray(),
            Links = snapshot.Links.Where(link => link.SourceResourceId != resourceId && link.TargetResourceId != resourceId).ToArray()
        };
        var impact = AnalyzeChange(snapshot, after, catalog, null, resourceId);
        var identityUses = snapshot.Links
            .Where(link => link.IdentityResourceId == resourceId)
            .Select(link => new IdentityUse(link.Id, link.SourceResourceId))
            .Concat(snapshot.Components
                .SelectMany(component => component.Resources)
                .Where(resource => resource.DefaultIdentityResourceId == resourceId)
                .Select(resource => new IdentityUse(null, resource.Id)))
            .Concat(snapshot.AppSettings
                .Where(setting => setting.IdentityResourceId == resourceId)
                .Select(setting => new IdentityUse(null, setting.ApplicationResourceId)))
            .Distinct()
            .ToArray();
        if (identityUses.Length == 0)
        {
            return impact;
        }

        var identityItems = identityUses.Select(use => new ImpactItem(
            "IMPACT-LIA-IDENTITE-UTILISEE",
            ImpactSeverity.Blocker,
            "Cette identité affectée est encore utilisée par le modèle et ne peut pas être supprimée.",
            use.LinkId,
            use.ResourceId));
        var items = impact.Items.Concat(identityItems)
            .OrderBy(item => item.Severity)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.LinkId)
            .ToArray();
        return new ImpactResult(ImpactSeverity.Blocker, items);
    }

    private ImpactResult AnalyzeChange(
        ModelSnapshot beforeSnapshot,
        ModelSnapshot afterSnapshot,
        CatalogVersion catalog,
        Guid? linkId,
        Guid resourceId)
    {
        var before = wiringEngine.Evaluate(beforeSnapshot, catalog);
        var after = wiringEngine.Evaluate(afterSnapshot, catalog);
        var retainedLinkIds = after.Links.Select(link => link.Link.Id).ToHashSet();
        var items = before.Links
            .Where(link => link.Link.Origin == LinkOrigin.Implicit && !retainedLinkIds.Contains(link.Link.Id))
            .Select(link => new ImpactItem(
                "IMPACT-LIA-LINK-IMPLICITE-RETIRE",
                ImpactSeverity.Information,
                "Une liaison implicite disparaîtra au prochain déploiement.",
                link.Link.Id,
                link.Link.SourceResourceId))
            .ToList();
        var retainedRoleNames = after.RoleAssignments.Select(assignment => assignment.DeterministicName)
            .ToHashSet(StringComparer.Ordinal);
        items.AddRange(before.RoleAssignments
            .Where(assignment => !retainedRoleNames.Contains(assignment.DeterministicName))
            .Select(assignment =>
            {
                var targetResourceId = assignment.TargetResourceIds.Count == 0
                    ? Guid.Empty
                    : assignment.TargetResourceIds[0];
                return new ImpactItem(
                    "IMPACT-LIA-ROLE-REVOKED",
                    ImpactSeverity.Critical,
                    RoleRevocationMessage,
                    linkId ?? assignment.Origins.Select(origin => (Guid?)origin.LinkId).FirstOrDefault(),
                    targetResourceId == Guid.Empty ? resourceId : targetResourceId);
            }));

        foreach (var requiredIssue in after.Issues.Where(issue => issue.Code == "VAL-LIA-OBLIGATOIRE"
            && !before.Issues.Any(previous => previous.Code == issue.Code
                && previous.ResourceId == issue.ResourceId
                && previous.Kind == issue.Kind)))
        {
            items.Add(new ImpactItem(
                "IMPACT-LIA-OBLIGATOIRE-ROMPU",
                ImpactSeverity.Blocker,
                "Une liaison obligatoire serait rompue.",
                linkId,
                requiredIssue.ResourceId));
        }

        var retainedDependencies = after.Dependencies.Select(dependency => dependency.LinkId).ToHashSet();
        items.AddRange(before.Dependencies
            .Where(dependency => !retainedDependencies.Contains(dependency.LinkId)
                && (linkId is null || dependency.LinkId == linkId))
            .Select(dependency => new ImpactItem(
                "IMPACT-LIA-DEPENDANCE-RETIREE",
                ImpactSeverity.Information,
                "Une dépendance de déploiement disparaîtra.",
                dependency.LinkId,
                resourceId)));

        var retainedDataAccessRoles = after.DataAccessRequests
            .SelectMany(request => request.DatabaseRoles.Select(role => DataAccessRoleKey.From(request, role)))
            .ToHashSet();
        foreach (var request in before.DataAccessRequests)
        {
            foreach (var role in request.DatabaseRoles.Where(role =>
                !retainedDataAccessRoles.Contains(DataAccessRoleKey.From(request, role))))
            {
                items.Add(new ImpactItem(
                    "IMPACT-LIA-DATA-ACCESS-REVOKED",
                    ImpactSeverity.Critical,
                    "Accès aux données révoqué dans la base au prochain déploiement.",
                    linkId ?? request.Origin.LinkId,
                    request.TargetResourceId));
            }
        }

        var severity = items.Any(item => item.Severity == ImpactSeverity.Blocker)
            ? ImpactSeverity.Blocker
            : items.Any(item => item.Severity == ImpactSeverity.Critical)
                ? ImpactSeverity.Critical
                : ImpactSeverity.Information;
        return new ImpactResult(severity, items.OrderBy(item => item.Severity)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.LinkId).ToArray());
    }

    private sealed record DataAccessRoleKey(
        Guid SourceResourceId,
        Guid TargetResourceId,
        DataEngine Engine,
        LinkIdentityKind IdentityKind,
        Guid? IdentityResourceId,
        string RoleName)
    {
        public static DataAccessRoleKey From(DataAccessRequest request, string roleName) => new(
            request.SourceResourceId,
            request.TargetResourceId,
            request.Engine,
            request.IdentityKind,
            request.IdentityResourceId,
            roleName);
    }

    private sealed record IdentityUse(Guid? LinkId, Guid ResourceId);
}
