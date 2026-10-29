using System.Text.Json;
using ErrorOr;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceLinkOptions;

public sealed class GetResourceLinkOptionsQueryHandler(
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider,
    LinkKindRegistry handlers)
    : IQueryHandler<GetResourceLinkOptionsQuery, ErrorOr<ResourceLinkOptionsSummary>>
{
    private const string RolesFieldName = "roles";
    private const string DataAccessLevelFieldName = "level";
    private static readonly Dictionary<string, DataAccessLevel> DataAccessLevels = new(StringComparer.Ordinal)
    {
        ["Lecture"] = DataAccessLevel.Lecture,
        ["LectureÉcriture"] = DataAccessLevel.LectureEcriture,
        ["Schéma"] = DataAccessLevel.Schema
    };

    public async ValueTask<ErrorOr<ResourceLinkOptionsSummary>> Handle(
        GetResourceLinkOptionsQuery query,
        CancellationToken cancellationToken)
    {
        var built = await snapshotBuilder.BuildAsync(query.ProjectId, cancellationToken);
        if (built.IsError)
        {
            return built.Errors;
        }

        var snapshot = built.Value;
        var sourceComponent = snapshot.Components.SingleOrDefault(component => component.Id == query.ComponentId);
        var sourceResource = sourceComponent?.Resources.SingleOrDefault(resource => resource.Id == query.ResourceId);
        if (sourceComponent is null || sourceResource is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        if (sourceResource.IsExisting)
        {
            return Error.Validation("LINK_EXISTING_SOURCE_INVALID", "An existing Azure resource cannot be the source of a link.");
        }

        var catalog = catalogProvider.GetVersion(snapshot.Project.CatalogVersion);
        var sourceDescriptor = catalog?.GetResourceType(sourceResource.Type);
        if (catalog is null || sourceDescriptor is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var resources = snapshot.Components
            .SelectMany(component => component.Resources.Select(resource => (Component: component, Resource: resource)))
            .Where(value => value.Resource.Id != sourceResource.Id)
            .ToArray();
        var linkCounts = snapshot.Links
            .Concat(handlers.Values.SelectMany(handler => handler.GetImplicitLinks(snapshot, catalog)))
            .GroupBy(link => (link.SourceResourceId, link.Kind))
            .ToDictionary(group => group.Key, group => group.Count());
        var options = handlers.Values
            .Where(handler =>
            {
                if (handler.IsImplicitOnly)
                {
                    return false;
                }

                var maximum = handler.GetCardinality(sourceDescriptor).Maximum;
                return maximum is null
                    || !linkCounts.TryGetValue((sourceResource.Id, handler.Key), out var count)
                    || count < maximum.Value;
            })
            .Select(handler =>
            {
                var fields = handler.Form.Fields;
                var targets = resources
                    .Select(value =>
                    {
                        var descriptor = catalog.GetResourceType(value.Resource.Type);
                        if (descriptor is null || !handler.Supports(sourceDescriptor, descriptor))
                        {
                            return null;
                        }

                        var targetIdentityRequirement = handler.GetIdentityRequirement(value.Resource.Properties);
                        var identities = GetIdentityOptions(
                            handler,
                            targetIdentityRequirement,
                            sourceComponent,
                            sourceResource,
                            sourceDescriptor);
                        var roles = fields.Any(field => string.Equals(field.Name, RolesFieldName, StringComparison.Ordinal))
                            ? descriptor.Definition.GetProperty("roles").EnumerateArray()
                                .Select(role => role.GetString()!)
                                .Where(role => !RoleAssignmentPolicy.IsForbiddenRole(role))
                                .Order(StringComparer.OrdinalIgnoreCase)
                                .ToArray()
                            : [];
                        var levels = fields.Any(field => string.Equals(field.Name, DataAccessLevelFieldName, StringComparison.Ordinal))
                            ? GetDataAccessLevels(descriptor.Definition)
                            : [];

                        return new ResourceLinkTargetOptionSummary(
                            value.Component.Id,
                            value.Component.Code,
                            value.Resource.Id,
                            value.Resource.LogicalName,
                            value.Resource.Type,
                            value.Resource.IsExisting,
                            roles,
                            levels,
                            identities);
                    })
                    .Where(target => target is not null)
                    .Select(target => target!)
                    .OrderBy(target => target.ComponentCode, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(target => target.ResourceName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return targets.Length == 0
                    ? null
                    : new ResourceLinkKindOptionSummary(handler.Key, handler.LabelFr, handler.LabelEn, fields, targets);
            })
            .Where(option => option is not null)
            .Select(option => option!)
            .OrderBy(option => option.LabelFr, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ResourceLinkOptionsSummary(options);
    }

    private static ResourceLinkIdentityOptionSummary[] GetIdentityOptions(
        ILinkKindHandler handler,
        LinkIdentityRequirement requirement,
        ComponentSnapshot sourceComponent,
        ResourceSnapshot sourceResource,
        CatalogResourceTypeDescriptor sourceDescriptor)
    {
        var options = new List<ResourceLinkIdentityOptionSummary>();
        var sourceActsAsIdentity = handler.SourceActsAsIdentity(sourceDescriptor);
        if (requirement == LinkIdentityRequirement.None
            || requirement == LinkIdentityRequirement.Optional
            || sourceActsAsIdentity)
        {
            options.Add(new ResourceLinkIdentityOptionSummary(LinkIdentityKind.None, null, null, false));
        }

        var identities = sourceDescriptor.Definition.GetProperty("identities");
        if (requirement is not (LinkIdentityRequirement.None or LinkIdentityRequirement.UserAssigned)
            && identities.GetProperty("system").GetBoolean())
        {
            options.Add(new ResourceLinkIdentityOptionSummary(LinkIdentityKind.System, null, null, false));
        }

        if (requirement != LinkIdentityRequirement.None
            && !sourceActsAsIdentity
            && identities.GetProperty("userAssignedMax").GetInt32() > 0)
        {
            options.AddRange(sourceComponent.Resources
                .Where(resource => string.Equals(resource.Type, "UserAssignedIdentity", StringComparison.Ordinal))
                .OrderBy(resource => resource.LogicalName, StringComparer.OrdinalIgnoreCase)
                .Select(resource => new ResourceLinkIdentityOptionSummary(
                    LinkIdentityKind.UserAssigned,
                    resource.Id,
                    resource.LogicalName,
                    sourceResource.DefaultIdentityResourceId == resource.Id)));
        }

        return options.ToArray();
    }

    private static DataAccessLevel[] GetDataAccessLevels(JsonElement definition)
    {
        if (!definition.TryGetProperty("dataAccess", out var dataAccess)
            || !dataAccess.TryGetProperty("levels", out var levels)
            || levels.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return levels.EnumerateArray()
            .Select(value => value.GetString())
            .Where(value => value is not null && DataAccessLevels.ContainsKey(value))
            .Select(value => DataAccessLevels[value!])
            .ToArray();
    }
}
