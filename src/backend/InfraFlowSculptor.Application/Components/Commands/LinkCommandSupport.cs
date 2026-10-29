using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands;

internal static class LinkCommandSupport
{
    private const string DataAccessLevelFieldName = "level";
    private const string RolesFieldName = "roles";
    private static readonly Dictionary<DataAccessLevel, string> CatalogDataAccessLevels = new()
    {
        [DataAccessLevel.Lecture] = "Lecture",
        [DataAccessLevel.LectureEcriture] = "LectureÉcriture",
        [DataAccessLevel.Schema] = "Schéma"
    };

    public static async Task<ErrorOr<LinkCommandContext>> LoadAndValidateAsync(
        IIfsDbContext dbContext,
        ICurrentOrganization currentOrganization,
        ICatalogProvider catalogProvider,
        LinkKindRegistry handlers,
        Guid projectId,
        Guid sourceComponentId,
        Guid sourceResourceId,
        Guid targetComponentId,
        Guid targetResourceId,
        LinkKind kind,
        LinkIdentityKind identityKind,
        Guid? identityResourceId,
        IReadOnlyList<string>? roles,
        LinkAccessScope accessScope,
        DataAccessLevel? dataAccessLevel,
        Guid? updatingLinkId,
        int? expectedComponentVersion,
        CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(
            dbContext, currentOrganization, projectId, sourceComponentId, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var (project, sourceComponent) = loaded.Value;
        if (expectedComponentVersion is { } expectedVersion)
        {
            var version = ResourceCommandSupport.CheckComponentVersion(sourceComponent, expectedVersion);
            if (version.IsError)
            {
                return version.Errors;
            }
        }

        var requestedRoles = roles ?? [];
        var sourceResource = sourceComponent.Resources.SingleOrDefault(resource => resource.Id == sourceResourceId);
        if (sourceResource is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The link source resource was not found.");
        }

        if (sourceResource.IsExisting)
        {
            return Error.Validation("LINK_EXISTING_SOURCE_INVALID", "An existing Azure resource cannot be the source of a link.");
        }

        var components = await dbContext.Query<ComponentAggregate>()
            .Where(component => component.ProjectId == new ProjectId(projectId))
            .ToArrayAsync(cancellationToken);
        var targetComponent = components.SingleOrDefault(component => component.Id.Value == targetComponentId);
        var targetResource = targetComponent?.Resources.SingleOrDefault(resource => resource.Id == targetResourceId);
        if (targetComponent is null || targetResource is null)
        {
            return Error.NotFound("LINK_TARGET_NOT_FOUND", "The link target was not found in this project.");
        }

        if (!Enum.IsDefined(kind) || !Enum.IsDefined(identityKind)
            || !Enum.IsDefined(accessScope)
            || (dataAccessLevel is not null && !Enum.IsDefined(dataAccessLevel.Value)))
        {
            return Error.Validation("LINK_PARAMETER_INVALID", "A link contains an unsupported parameter value.");
        }

        var catalog = catalogProvider.GetVersion(project.CatalogVersion);
        var sourceDescriptor = catalog?.GetResourceType(sourceResource.Type);
        var targetDescriptor = catalog?.GetResourceType(targetResource.Type);
        if (catalog is null || sourceDescriptor is null || targetDescriptor is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        if (!handlers.Supports(kind))
        {
            return Error.Validation("LINK_KIND_UNSUPPORTED", "This link kind is not available in the current plan step.");
        }

        var handler = handlers.Get(kind);
        if (handler.IsImplicitOnly)
        {
            return Error.Validation("LINK_IMPLICIT_ONLY", "This link is derived automatically and cannot be edited directly.");
        }

        if (!handler.Supports(sourceDescriptor, targetDescriptor))
        {
            return Error.Validation("LINK_TYPE_UNSUPPORTED", "The pinned catalog does not allow this source and target for the selected link kind.");
        }

        var supportsDataAccessLevel = handler.Form.Fields.Any(field => string.Equals(field.Name, DataAccessLevelFieldName, StringComparison.Ordinal));
        if (dataAccessLevel is not null && !supportsDataAccessLevel)
        {
            return Error.Validation("LINK_DATA_ACCESS_LEVEL_UNSUPPORTED", "A data access level is only valid for a data access link.");
        }

        var supportsRoles = handler.Form.Fields.Any(field => string.Equals(field.Name, RolesFieldName, StringComparison.Ordinal));
        if (requestedRoles.Count > 0 && !supportsRoles)
        {
            return Error.Validation("LINK_ROLE_UNSUPPORTED", "Roles can only be selected for a link kind that supports role assignments.");
        }

        var links = await dbContext.Query<LinkAggregate>()
            .Where(link => link.ProjectId == new ProjectId(projectId)
                && link.SourceResourceId == sourceResourceId)
            .ToArrayAsync(cancellationToken);
        var matchingKindLinks = links.ToLookup(link => link.Kind)[kind]
            .Where(link => link.Id != updatingLinkId)
            .ToArray();
        var cardinality = handler.GetCardinality(sourceDescriptor);
        if (cardinality.Maximum is { } maximum && matchingKindLinks.Length >= maximum)
        {
            return Error.Validation("LINK_CARDINALITY_EXCEEDED", $"This source allows at most {maximum} link(s) of this kind.");
        }

        var identityRequirement = handler.GetIdentityRequirement(targetResource.GetProperties());
        var identityError = ValidateIdentity(
            handler, identityRequirement, sourceComponent, sourceResource, sourceDescriptor, components,
            identityKind, identityResourceId);
        if (identityError is { } identityValidationError)
        {
            return identityValidationError;
        }

        var allowedRoles = targetDescriptor.Definition.GetProperty("roles").EnumerateArray()
            .Select(role => role.GetString()!).ToArray();
        if (requestedRoles.Any(role => string.IsNullOrWhiteSpace(role)
            || !allowedRoles.Contains(role.Trim(), StringComparer.OrdinalIgnoreCase)
            || RoleAssignmentPolicy.IsForbiddenRole(role)))
        {
            return Error.Validation("LINK_ROLE_INVALID", "Only non-privileged roles declared by the target descriptor can be selected.");
        }

        if (handler.Form.Fields.Any(field => string.Equals(field.Name, RolesFieldName, StringComparison.Ordinal) && field.Required)
            && requestedRoles.Count == 0)
        {
            return Error.Validation("LINK_ROLE_REQUIRED", "At least one role is required for this link.");
        }

        if (handler.Form.Fields.Any(field => string.Equals(field.Name, DataAccessLevelFieldName, StringComparison.Ordinal) && field.Required)
            && dataAccessLevel is null)
        {
            return Error.Validation("LINK_DATA_ACCESS_LEVEL_REQUIRED", "A data access level is required for this link.");
        }

        if (dataAccessLevel is { } level)
        {
            var dataAccess = targetDescriptor.Definition.GetProperty("dataAccess");
            if (!dataAccess.TryGetProperty("levels", out var levels) || levels.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return Error.Validation("LINK_DATA_ACCESS_LEVEL_INVALID", "The target database does not declare supported data access levels.");
            }

            var allowedLevels = levels.EnumerateArray().Select(value => value.GetString()!).ToArray();
            var catalogLevel = CatalogDataAccessLevels.GetValueOrDefault(level, string.Empty);
            if (!allowedLevels.Contains(catalogLevel, StringComparer.Ordinal))
            {
                return Error.Validation("LINK_DATA_ACCESS_LEVEL_INVALID", "The selected data access level is not supported by the target database.");
            }
        }

        var normalizedRoleNames = NormalizeRoleNames(requestedRoles);
        var normalizedRoles = normalizedRoleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (normalizedRoles.Count != normalizedRoleNames.Length)
        {
            return Error.Validation("LINK_ROLE_DUPLICATE", "A role can only be selected once on a link.");
        }

        var hasDuplicate = matchingKindLinks.Any(link =>
            link.TargetComponentId == targetComponentId
            && link.TargetResourceId == targetResourceId
            && link.IdentityKind == identityKind
            && link.IdentityResourceId == identityResourceId
            && link.AccessScope == accessScope
            && link.DataAccessLevel == dataAccessLevel
            && link.Roles.Count == normalizedRoles.Count
            && link.Roles.Select(role => role.Name).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(normalizedRoles));
        if (hasDuplicate)
        {
            return Error.Validation("VAL-LIA-DOUBLON", "An identical link already exists for this source resource.");
        }

        return new LinkCommandContext(project, sourceComponent, sourceResource, targetComponent, targetResource);
    }

    public static ErrorOr<Success> CheckLinkVersion(LinkAggregate link, int expectedVersion)
    {
        if (link.Version != expectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The link changed. Reload it before saving.",
                new Dictionary<string, object> { ["currentVersion"] = link.Version });
        }

        return Result.Success;
    }

    public static string[] NormalizeRoleNames(IEnumerable<string>? roles) => (roles ?? [])
        .Select(role => role.Trim())
        .OrderBy(role => role, StringComparer.Ordinal)
        .ToArray();

    public static void RecordComponentLinkChanges(params ComponentAggregate?[] components)
    {
        var changedComponentIds = new HashSet<ComponentId>();
        foreach (var component in components)
        {
            if (component is not null && changedComponentIds.Add(component.Id))
            {
                component.RecordLinkChange();
            }
        }
    }

    private static Error? ValidateIdentity(
        ILinkKindHandler handler,
        LinkIdentityRequirement identityRequirement,
        ComponentAggregate sourceComponent,
        Domain.ComponentAggregate.Resource sourceResource,
        CatalogResourceTypeDescriptor sourceDescriptor,
        IReadOnlyList<ComponentAggregate> components,
        LinkIdentityKind identityKind,
        Guid? identityResourceId)
    {
        var sourceActsAsIdentity = identityKind == LinkIdentityKind.None && handler.SourceActsAsIdentity(sourceDescriptor);
        if (identityRequirement == LinkIdentityRequirement.None)
        {
            return identityKind == LinkIdentityKind.None
                ? null
                : Error.Validation("LINK_IDENTITY_UNSUPPORTED", "This link kind does not use a managed identity.");
        }

        if (identityRequirement is LinkIdentityRequirement.Required or LinkIdentityRequirement.UserAssigned
            && identityKind == LinkIdentityKind.None && !sourceActsAsIdentity)
        {
            return Error.Validation("LINK_IDENTITY_REQUIRED", "This link kind requires a managed identity.");
        }

        if (identityRequirement == LinkIdentityRequirement.UserAssigned
            && identityKind != LinkIdentityKind.UserAssigned)
        {
            return Error.Validation("LINK_USER_ASSIGNED_IDENTITY_REQUIRED", "This link kind requires a user-assigned identity.");
        }

        if (identityKind == LinkIdentityKind.System
            && !sourceDescriptor.Definition.GetProperty("identities").GetProperty("system").GetBoolean())
        {
            return Error.Validation("LINK_SYSTEM_IDENTITY_UNSUPPORTED", "The source type does not support a system-assigned identity.");
        }

        if (identityKind != LinkIdentityKind.UserAssigned)
        {
            return null;
        }

        if (!sourceActsAsIdentity
            && sourceDescriptor.Definition.GetProperty("identities").GetProperty("userAssignedMax").GetInt32() == 0)
        {
            return Error.Validation("LINK_USER_ASSIGNED_IDENTITY_UNSUPPORTED", "The source type does not support a user-assigned identity.");
        }

        var selectedIdentity = components.SelectMany(component => component.Resources
                .Select(resource => (Component: component, Resource: resource)))
            .SingleOrDefault(candidate => candidate.Resource.Id == identityResourceId);
        if (selectedIdentity.Resource is null
            || !string.Equals(selectedIdentity.Resource.Type, "UserAssignedIdentity", StringComparison.Ordinal))
        {
            return Error.Validation("LINK_USER_ASSIGNED_IDENTITY_NOT_FOUND", "Select a user-assigned identity in this project.");
        }

        if (selectedIdentity.Component.Id != sourceComponent.Id)
        {
            return Error.Validation("VAL-LIA-IDENTITE-PARTAGEE", "A user-assigned identity can only serve resources in its own deployment component.");
        }

        if (sourceActsAsIdentity && identityResourceId is not null
            || sourceResource.Type == "UserAssignedIdentity" && identityResourceId is not null)
        {
            return Error.Validation("LINK_IDENTITY_PARAMETER_INVALID", "A user-assigned identity used as the link source must not be selected a second time.");
        }

        return null;
    }

}

internal sealed record LinkCommandContext(
    ProjectAggregate Project,
    ComponentAggregate SourceComponent,
    Domain.ComponentAggregate.Resource SourceResource,
    ComponentAggregate TargetComponent,
    Domain.ComponentAggregate.Resource TargetResource);
