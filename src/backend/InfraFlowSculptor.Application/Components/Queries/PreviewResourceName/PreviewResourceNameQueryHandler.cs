using ErrorOr;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.PreviewResourceName;

public sealed class PreviewResourceNameQueryHandler(
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider)
    : IQueryHandler<PreviewResourceNameQuery, ErrorOr<NamingPreviewResult>>
{
    public async ValueTask<ErrorOr<NamingPreviewResult>> Handle(
        PreviewResourceNameQuery query,
        CancellationToken cancellationToken)
    {
        if (query.ResourceGroupId == Guid.Empty
            || string.IsNullOrWhiteSpace(query.Type)
            || string.IsNullOrWhiteSpace(query.LogicalName))
        {
            return Error.Validation("RESOURCE_NAME_PREVIEW_INPUT_INVALID", "Resource group, type and logical name are required.");
        }

        var snapshotResult = await snapshotBuilder.BuildWithOrganizationNamingReservationsAsync(
            query.ProjectId, cancellationToken);
        if (snapshotResult.IsError)
        {
            return snapshotResult.Errors;
        }

        var snapshot = snapshotResult.Value;
        var catalog = catalogProvider.GetVersion(snapshot.Project.CatalogVersion);
        if (catalog?.GetResourceType(query.Type) is null)
        {
            return Error.Validation("RESOURCE_TYPE_UNKNOWN", "The resource type is not present in the project's pinned catalogue.");
        }

        var component = snapshot.Components.SingleOrDefault(candidate => candidate.Id == query.ComponentId);
        if (component is null)
        {
            return Error.NotFound("COMPONENT_NOT_FOUND", "The component was not found.");
        }

        if (!component.ResourceGroups.Any(group => group.Id == query.ResourceGroupId))
        {
            return Error.Validation("RESOURCE_GROUP_NOT_FOUND", "The resource group must belong to the component.");
        }

        Guid resourceId;
        ModelSnapshot namingSnapshot;
        if (query.ResourceId is Guid existingResourceId)
        {
            var existingResource = component.Resources.SingleOrDefault(resource => resource.Id == existingResourceId);
            if (existingResource is null)
            {
                return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found in the component.");
            }

            if (existingResource.ResourceGroupId != query.ResourceGroupId
                || existingResource.Type != query.Type
                || !string.Equals(existingResource.LogicalName, query.LogicalName.Trim(), StringComparison.Ordinal))
            {
                return Error.Validation("RESOURCE_NAME_PREVIEW_RESOURCE_MISMATCH", "The preview inputs must match the existing resource.");
            }

            resourceId = existingResourceId;
            namingSnapshot = snapshot;
        }
        else
        {
            resourceId = Guid.NewGuid();
            var candidate = new ResourceSnapshot(
                resourceId,
                query.ResourceGroupId,
                query.Type,
                query.LogicalName.Trim(),
                string.Empty,
                IsExisting: false,
                new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal),
                new Dictionary<Guid, IReadOnlyDictionary<string, System.Text.Json.JsonElement>>(),
                new Dictionary<Guid, bool>(),
                new Dictionary<Guid, string>(),
                new Dictionary<Guid, string>(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                []);
            var updatedComponent = component with { Resources = [.. component.Resources, candidate] };
            namingSnapshot = snapshot with
            {
                Components = snapshot.Components
                    .Select(value => value.Id == component.Id ? updatedComponent : value)
                    .ToArray()
            };
        }

        var preview = NamingEngine.Preview(namingSnapshot);
        return preview with
        {
            Items = preview.Items.Where(item => item.ResourceId == resourceId).ToArray(),
            Issues = preview.Issues.Where(issue => issue.ResourceId == resourceId || issue.ResourceGroupId == query.ResourceGroupId).ToArray()
        };
    }
}
