using ErrorOr;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Tags;
using InfraFlowSculptor.Engine.Targets;
using InfraFlowSculptor.Engine.Values;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetEffectiveValues;

public sealed class GetEffectiveValuesQueryHandler(
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider)
    : IQueryHandler<GetEffectiveValuesQuery, ErrorOr<IReadOnlyList<EffectiveValueTargetSummary>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<EffectiveValueTargetSummary>>> Handle(
        GetEffectiveValuesQuery query,
        CancellationToken cancellationToken)
    {
        var snapshotResult = await snapshotBuilder.BuildAsync(query.ProjectId, cancellationToken);
        if (snapshotResult.IsError)
        {
            return snapshotResult.Errors;
        }

        var snapshot = snapshotResult.Value;
        var component = snapshot.Components.SingleOrDefault(candidate =>
            candidate.Resources.Any(resource => resource.Id == query.ResourceId));
        if (component is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        var resource = component.Resources.Single(candidate => candidate.Id == query.ResourceId);
        var resourceGroup = component.ResourceGroups.SingleOrDefault(candidate => candidate.Id == resource.ResourceGroupId);
        if (resourceGroup is null)
        {
            return Error.Unexpected("RESOURCE_GROUP_NOT_FOUND", "The resource group for this resource was not found.");
        }

        var descriptor = catalogProvider.GetVersion(snapshot.Project.CatalogVersion)?.GetResourceType(resource.Type);
        if (descriptor is null)
        {
            return Error.Unexpected("RESOURCE_CATALOG_TYPE_NOT_FOUND", "The resource type is missing from the project's catalog version.");
        }

        var targets = TargetResolver.ResolveResourceTargets(snapshot, component, resource);
        return targets.Select(target => new EffectiveValueTargetSummary(
            target.Target.Id,
            target.Target.Code,
            target.IsPresent,
            target.Region,
            TagResolver.Resolve(snapshot, component, resourceGroup, resource, target),
            EffectiveValueResolver.Resolve(resource, target.Target.Id, descriptor.Definition)
                .Select(property => new EffectivePropertySummary(
                    property.Name,
                    property.Value,
                    ProvenanceCode(property.Provenance)))
                .ToArray())).ToArray();
    }

    private static string ProvenanceCode(EffectiveValueProvenance provenance) => provenance switch
    {
        EffectiveValueProvenance.Override => "override",
        EffectiveValueProvenance.Resource => "resource",
        EffectiveValueProvenance.CatalogDefault => "catalogDefault",
        EffectiveValueProvenance.Absent => "absent",
        _ => throw new ArgumentOutOfRangeException(nameof(provenance), provenance, null)
    };
}
