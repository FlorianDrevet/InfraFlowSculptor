using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;
using InfraFlowSculptor.Engine.Ordering;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Applications;

namespace InfraFlowSculptor.Engine.Validation;

public sealed record ValidationContext(
    ModelSnapshot Snapshot,
    CatalogVersion Catalog,
    ComponentOrderingResult Ordering,
    NamingPreviewResult Naming,
    LinkKindRegistry LinkKinds,
    DeploymentStrategyRegistry DeploymentStrategies)
{
    public string Lot { get; init; } = "lot1";

    public IEnumerable<(ComponentSnapshot Component, ResourceSnapshot Resource)> Resources => Snapshot.Components
        .SelectMany(component => component.Resources.Select(resource => (component, resource)));

    public bool TryGetResource(Guid resourceId, out ComponentSnapshot component, out ResourceSnapshot resource)
    {
        foreach (var item in Resources)
        {
            if (item.Resource.Id == resourceId)
            {
                component = item.Component;
                resource = item.Resource;
                return true;
            }
        }

        component = null!;
        resource = null!;
        return false;
    }

    public int? GetObjectVersion(ObjectRef objectRef) => Snapshot.ObjectVersions.TryGetValue(objectRef, out var version)
        ? version
        : null;

    public IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> ResourceTypes { get; } =
        Catalog.ResourceTypes.ToDictionary(descriptor => descriptor.Type, StringComparer.OrdinalIgnoreCase);

    public ValidationFinding Finding(
        string code,
        FindingSeverity severity,
        ObjectRef objectRef,
        string messageFr,
        string messageEn,
        Guid? environmentId = null,
        string? fixCode = null,
        IReadOnlyList<Guid>? relatedLinkIds = null,
        string? detailKey = null,
        int? objectVersion = null) => new(
        code,
        severity,
        Lot,
        objectRef,
        environmentId,
        messageFr,
        messageEn,
        objectVersion ?? GetObjectVersion(objectRef),
        fixCode,
        relatedLinkIds ?? [],
        detailKey);
}
