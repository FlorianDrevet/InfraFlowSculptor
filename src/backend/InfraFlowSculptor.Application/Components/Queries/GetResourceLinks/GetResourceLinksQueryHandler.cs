using ErrorOr;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceLinks;

public sealed class GetResourceLinksQueryHandler(
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider,
    WiringEngine wiringEngine,
    LinkKindRegistry linkKinds)
    : IQueryHandler<GetResourceLinksQuery, ErrorOr<ResourceLinksSummary>>
{
    public async ValueTask<ErrorOr<ResourceLinksSummary>> Handle(
        GetResourceLinksQuery query,
        CancellationToken cancellationToken)
    {
        var built = await snapshotBuilder.BuildAsync(query.ProjectId, cancellationToken);
        if (built.IsError)
        {
            return built.Errors;
        }

        var snapshot = built.Value;
        var currentComponent = snapshot.Components.SingleOrDefault(component => component.Id == query.ComponentId);
        if (currentComponent is null || !currentComponent.Resources.Any(resource => resource.Id == query.ResourceId))
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        var catalog = catalogProvider.GetVersion(snapshot.Project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var result = wiringEngine.Evaluate(snapshot, catalog);
        var components = snapshot.Components.ToDictionary(component => component.Id);
        var resources = snapshot.Components.SelectMany(component => component.Resources
                .Select(resource => (Component: component, Resource: resource)))
            .ToDictionary(entry => entry.Resource.Id);
        var outgoing = new List<ResourceLinkSummary>();
        var incoming = new List<ResourceLinkSummary>();

        var wiredById = result.Links.ToDictionary(wired => wired.Link.Id);
        var links = snapshot.Links
            .Select(link => wiredById.TryGetValue(link.Id, out var wired)
                ? wired
                : CreateUnwiredLink(link, resources))
            .Concat(result.Links.Where(wired => wired.Link.Origin == LinkOrigin.Implicit))
            .DistinctBy(wired => wired.Link.Id);

        foreach (var wiredLink in links)
        {
            var link = wiredLink.Link;
            var hasSource = resources.TryGetValue(link.SourceResourceId, out var source);
            var hasTarget = resources.TryGetValue(link.TargetResourceId, out var target);
            var sourceComponentCode = components.TryGetValue(link.SourceComponentId, out var sourceComponent)
                ? sourceComponent.Code
                : link.SourceComponentId.ToString("D");
            var targetComponentCode = components.TryGetValue(link.TargetComponentId, out var targetComponent)
                ? targetComponent.Code
                : link.TargetComponentId.ToString("D");

            var roles = result.RoleAssignments
                .Where(assignment => assignment.Origins.Any(origin => origin.LinkId == link.Id))
                .Select(assignment => assignment.RoleName)
                .Concat(link.Roles)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var summary = new ResourceLinkSummary(
                link.Id,
                link.Version,
                link.Kind,
                linkKinds.Supports(link.Kind) ? linkKinds.Get(link.Kind).LabelFr : link.Kind.ToString(),
                linkKinds.Supports(link.Kind) ? linkKinds.Get(link.Kind).LabelEn : link.Kind.ToString(),
                link.Origin,
                link.SourceComponentId,
                sourceComponentCode,
                link.SourceResourceId,
                hasSource ? source.Resource.LogicalName : link.SourceResourceId.ToString("D"),
                link.TargetComponentId,
                targetComponentCode,
                link.TargetResourceId,
                hasTarget ? target.Resource.LogicalName : link.TargetResourceId.ToString("D"),
                link.IdentityKind,
                link.IdentityResourceId,
                link.AccessScope,
                link.DataAccessLevel,
                roles,
                wiredLink.IsExternal || (hasTarget && target.Resource.IsExisting),
                wiredLink.CreatesDeploymentDependency,
                result.Issues.Where(issue => issue.LinkId == link.Id)
                    .Select(issue => new ResourceLinkIssueSummary(issue.Code, issue.Message))
                    .ToArray(),
                link.OriginSource);

            if (link.SourceResourceId == query.ResourceId)
            {
                outgoing.Add(summary);
            }

            if (link.TargetResourceId == query.ResourceId)
            {
                incoming.Add(summary);
            }
        }

        return new ResourceLinksSummary(
            OrderLinks(outgoing),
            OrderLinks(incoming));
    }

    private static ResourceLinkSummary[] OrderLinks(IEnumerable<ResourceLinkSummary> links) => links
        .OrderBy(link => link.Kind)
        .ThenBy(link => link.SourceResourceName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(link => link.TargetResourceName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(link => link.Id)
        .ToArray();

    private static WiredLink CreateUnwiredLink(
        LinkSnapshot link,
        Dictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resources)
    {
        var isExternal = link.SourceComponentId != link.TargetComponentId;
        if (resources.TryGetValue(link.TargetResourceId, out var target))
        {
            isExternal |= target.Resource.IsExisting;
        }

        return new WiredLink(link, isExternal, CreatesDeploymentDependency: false);
    }
}
