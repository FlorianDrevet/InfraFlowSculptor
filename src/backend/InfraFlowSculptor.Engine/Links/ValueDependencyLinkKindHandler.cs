using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Links;

public sealed class ValueDependencyLinkKindHandler() : LinkKindHandler(
    LinkKind.ValueDependency,
    isImplicitOnly: true,
    createsDeploymentDependency: true,
    LinkIdentityRequirement.None,
    new LinkFormDefinition([]))
{
    protected override bool SupportsSource(CatalogResourceTypeDescriptor source) =>
        !string.Equals(source.Definition.GetProperty("applicationSettings").GetProperty("destination").GetString(),
            "none", StringComparison.Ordinal);

    protected override bool SupportsTarget(CatalogResourceTypeDescriptor target) =>
        target.Definition.GetProperty("outputs").GetArrayLength() > 0;

    protected override bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target) =>
        target.Definition.GetProperty("outputs").GetArrayLength() > 0;

    public override IReadOnlyList<LinkSnapshot> GetImplicitLinks(ModelSnapshot snapshot, CatalogVersion catalog)
    {
        var resources = snapshot.Components.SelectMany(component => component.Resources
            .Select(resource => (Component: component, Resource: resource)))
            .ToDictionary(entry => entry.Resource.Id);

        return snapshot.AppSettings
            .Where(setting => setting.SourceKind == InfraFlowSculptor.Domain.ComponentAggregate.AppSettingSourceKind.ResourceOutput
                && setting.SourceResourceId is not null)
            .OrderBy(setting => setting.Id)
            .Where(setting => resources.ContainsKey(setting.ApplicationResourceId)
                && resources.ContainsKey(setting.SourceResourceId!.Value))
            .Select(setting =>
            {
                var consumer = resources[setting.ApplicationResourceId];
                var source = resources[setting.SourceResourceId!.Value];
                return new LinkSnapshot(
                    LinkIdFactory.Create($"appSettingValueDependency:{setting.Id:N}", consumer.Resource.Id, source.Resource.Id),
                    consumer.Component.Id,
                    consumer.Resource.Id,
                    source.Component.Id,
                    source.Resource.Id,
                    Key,
                    LinkOrigin.Implicit,
                    LinkIdentityKind.None,
                    null,
                    [],
                    LinkAccessScope.Resource,
                    null);
            })
            .ToArray();
    }
}
