using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Links;

public sealed class DiagnosticsLinkKindHandler() : LinkKindHandler(
    LinkKind.Diagnostics,
    labelFr: "Diagnostics",
    labelEn: "Diagnostics",
    isImplicitOnly: false,
    createsDeploymentDependency: true,
    LinkIdentityRequirement.None,
    new LinkFormDefinition([new LinkFormField("excludedCategories", "array", false)]))
{
    public override int? MaximumFor(CatalogResourceTypeDescriptor source) => 1;

    protected override bool SupportsSource(CatalogResourceTypeDescriptor source) =>
        source.Definition.GetProperty("diagnostics").GetProperty("supported").GetBoolean();

    protected override bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target) =>
        target.Type == "LogAnalyticsWorkspace"
        && target.Definition.GetProperty("links").GetProperty("asTarget")
            .EnumerateArray()
            .Any(kind => string.Equals(kind.GetString(), Key.ToString(), StringComparison.Ordinal));

    public override IReadOnlyList<LinkSnapshot> GetImplicitLinks(ModelSnapshot snapshot, CatalogVersion catalog)
    {
        var resources = snapshot.Components.SelectMany(component => component.Resources
            .Select(resource => (Component: component, Resource: resource)))
            .ToDictionary(entry => entry.Resource.Id);
        var linksBySource = snapshot.Links.ToLookup(link => (link.SourceResourceId, link.Kind));
        var result = new List<LinkSnapshot>();

        foreach (var component in snapshot.Components.OrderBy(value => value.Id))
        {
            if (component.DefaultLogAnalyticsResourceId is not { } workspaceId
                || !resources.TryGetValue(workspaceId, out var workspace))
            {
                continue;
            }

            var targetDescriptor = catalog.GetResourceType(workspace.Resource.Type);
            if (targetDescriptor is null)
            {
                continue;
            }

            foreach (var source in component.Resources.OrderBy(resource => resource.Id))
            {
                var sourceDescriptor = catalog.GetResourceType(source.Type);
                if (source.Id == workspaceId || source.IsExisting
                    || sourceDescriptor is null || !Supports(sourceDescriptor, targetDescriptor)
                    || linksBySource[(source.Id, Key)].Any())
                {
                    continue;
                }

                var implicitLink = new LinkSnapshot(
                    LinkIdFactory.Create("defaultDiagnostics", source.Id, workspaceId),
                    component.Id,
                    source.Id,
                    workspace.Component.Id,
                    workspaceId,
                    Key,
                    LinkOrigin.Implicit,
                    LinkIdentityKind.None,
                    null,
                    [],
                    LinkAccessScope.Resource,
                    null,
                    OriginSource: new LinkOriginSource(
                        LinkOriginSourceKind.ComponentDefaultLogAnalytics,
                        component.Id,
                        component.Code));
                result.Add(implicitLink);
            }
        }

        return result;
    }
}
