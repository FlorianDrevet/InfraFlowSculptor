using System.Text.RegularExpressions;
using System.Text.Json;
using ErrorOr;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.ComponentAggregate;

[AuditedObject(ObjectType.Resource, nameof(Resource.Id), nameof(Resource.OrganizationId))]
public sealed partial class Resource : IOrganizationOwned, IHasVersion
{
    private static readonly HashSet<string> ReservedTagKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ifs-project", "ifs-component", "ifs-environment", "managed-by"
    };

    private readonly List<ResourceValueNode> values = [];
    private readonly List<ResourcePresence> presences = [];
    private readonly List<ResourceExistingId> existingIds = [];
    private readonly List<ResourceNameOverride> forcedNames = [];
    private readonly List<ResourceChild> children = [];
    private readonly List<ResourceTag> tags = [];

    public Guid Id { get; private set; }

    public ProjectId ProjectId { get; private set; }

    public OrganizationId OrganizationId { get; private set; }

    public Guid ResourceGroupId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string LogicalName { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool IsExisting { get; private set; }

    public Guid? DefaultIdentityResourceId { get; private set; }

    public int Version { get; private set; } = 1;

    public IReadOnlyCollection<ResourceValueNode> Values => values.AsReadOnly();

    public IReadOnlyCollection<ResourcePresence> Presences => presences.AsReadOnly();

    public IReadOnlyCollection<ResourceExistingId> ExistingIds => existingIds.AsReadOnly();

    public IReadOnlyCollection<ResourceNameOverride> ForcedNames => forcedNames.AsReadOnly();

    public IReadOnlyCollection<ResourceChild> Children => children.AsReadOnly();

    public IReadOnlyCollection<ResourceTag> Tags => tags.AsReadOnly();

    private Resource()
    {
    }

    private Resource(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        Guid resourceGroupId,
        string type,
        string logicalName,
        string description,
        bool isExisting)
    {
        Id = id;
        ProjectId = projectId;
        OrganizationId = organizationId;
        ResourceGroupId = resourceGroupId;
        Type = type;
        LogicalName = logicalName;
        Description = description;
        IsExisting = isExisting;
    }

    internal static ErrorOr<Resource> Create(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        Guid resourceGroupId,
        string type,
        string logicalName,
        string? description,
        bool isExisting,
        IEnumerable<Guid> targetEnvironmentIds,
        IReadOnlyDictionary<string, JsonElement> properties,
        IEnumerable<KeyValuePair<string, string>>? tags = null,
        Guid? defaultIdentityResourceId = null)
    {
        var normalizedType = type?.Trim() ?? string.Empty;
        var normalizedName = logicalName?.Trim() ?? string.Empty;
        var normalizedDescription = description?.Trim() ?? string.Empty;
        if (id == Guid.Empty || projectId.Value == Guid.Empty || organizationId.Value == Guid.Empty
            || resourceGroupId == Guid.Empty || normalizedType.Length is < 1 or > 100
            || defaultIdentityResourceId == Guid.Empty)
        {
            return Error.Validation("RESOURCE_IDENTITY_INVALID", "A resource identifier, group, and descriptor type are required.");
        }

        if (!LogicalNamePattern().IsMatch(normalizedName))
        {
            return Error.Validation("RESOURCE_LOGICAL_NAME_INVALID", "The logical name must match ^[a-z][a-z0-9-]{0,39}$.");
        }

        if (normalizedDescription.Length > 500)
        {
            return Error.Validation("RESOURCE_DESCRIPTION_INVALID", "A resource description cannot exceed 500 characters.");
        }

        if (isExisting && properties.Count > 0)
        {
            return Error.Validation("RESOURCE_EXISTING_PROPERTIES_INVALID", "An existing resource cannot define deployable properties.");
        }

        var environments = targetEnvironmentIds.Distinct().ToArray();
        if (environments.Any(environmentId => environmentId == Guid.Empty))
        {
            return Error.Validation("RESOURCE_TARGET_INVALID", "Every resource target must be a project environment.");
        }

        var resource = new Resource(
            id, projectId, organizationId, resourceGroupId, normalizedType, normalizedName, normalizedDescription, isExisting)
        {
            DefaultIdentityResourceId = defaultIdentityResourceId
        };
        foreach (var environmentId in environments)
        {
            resource.presences.Add(new ResourcePresence(environmentId, !isExisting));
        }

        var set = resource.SetProperties(properties);
        if (set.IsError)
        {
            return set.Errors;
        }

        var setTags = resource.SetTags(tags ?? []);
        if (setTags.IsError)
        {
            return setTags.Errors;
        }

        return resource;
    }

    internal ErrorOr<Success> SetProperties(IReadOnlyDictionary<string, JsonElement> properties)
    {
        if (IsExisting && properties.Count > 0)
        {
            return Error.Validation("RESOURCE_EXISTING_PROPERTIES_INVALID", "An existing resource cannot define deployable properties.");
        }

        try
        {
            var replacements = new List<ResourceValueNode>();
            foreach (var property in properties)
            {
                replacements.AddRange(ResourceValueNode.CreateTree(property.Key, property.Value, null));
            }

            foreach (var propertyName in properties.Keys)
            {
                RemoveTree(null, propertyName);
            }

            values.AddRange(replacements);
        }
        catch (ArgumentException exception)
        {
            return Error.Validation("RESOURCE_PROPERTY_VALUE_INVALID", exception.Message);
        }

        Touch();
        return Result.Success;
    }

    internal ErrorOr<Success> SetOverride(Guid environmentId, string propertyName, JsonElement value)
    {
        if (!presences.Any(presence => presence.TargetId == environmentId))
        {
            return Error.Validation("RESOURCE_OVERRIDE_TARGET_INVALID", "An override must target an environment selected by the component.");
        }

        try
        {
            ReplaceValue(environmentId, propertyName, value);
        }
        catch (ArgumentException exception)
        {
            return Error.Validation("RESOURCE_PROPERTY_VALUE_INVALID", exception.Message);
        }

        Touch();
        return Result.Success;
    }

    internal ErrorOr<Success> ClearOverride(Guid environmentId, string propertyName)
    {
        if (!presences.Any(presence => presence.TargetId == environmentId))
        {
            return Error.Validation("RESOURCE_OVERRIDE_TARGET_INVALID", "An override must target an environment selected by the component.");
        }

        RemoveTree(environmentId, propertyName);
        Touch();
        return Result.Success;
    }

    internal ErrorOr<Success> SetPresence(Guid environmentId, bool isPresent)
    {
        var presence = presences.SingleOrDefault(candidate => candidate.TargetId == environmentId);
        if (presence is null)
        {
            return Error.Validation("RESOURCE_PRESENCE_TARGET_INVALID", "Presence can only be set for an environment selected by the component.");
        }

        if (isPresent && IsExisting && !existingIds.Any(item => item.TargetId == environmentId))
        {
            return Error.Validation("RESOURCE_EXISTING_ID_REQUIRED", "An existing resource needs an Azure resource identifier before it can be present.");
        }

        presence.Set(isPresent);
        Touch();
        return Result.Success;
    }

    internal ErrorOr<Success> SetExistingResourceId(Guid environmentId, string value)
    {
        if (!IsExisting)
        {
            return Error.Validation("RESOURCE_NOT_EXISTING", "Only an existing resource can have an Azure resource identifier.");
        }

        var presence = presences.SingleOrDefault(candidate => candidate.TargetId == environmentId);
        if (presence is null)
        {
            return Error.Validation("RESOURCE_EXISTING_ID_TARGET_INVALID", "An identifier must target an environment selected by the component.");
        }

        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 2048)
        {
            return Error.Validation("RESOURCE_EXISTING_ID_INVALID", "An Azure resource identifier must contain between 1 and 2048 characters.");
        }

        var existing = existingIds.SingleOrDefault(item => item.TargetId == environmentId);
        if (existing is null)
        {
            existingIds.Add(new ResourceExistingId(environmentId, normalized));
        }
        else
        {
            existing.Set(normalized);
        }

        presence.Set(true);
        Touch();
        return Result.Success;
    }

    internal ErrorOr<Success> SetForcedName(Guid environmentId, string name)
    {
        if (!presences.Any(presence => presence.TargetId == environmentId))
        {
            return Error.Validation("RESOURCE_FORCED_NAME_TARGET_INVALID", "A forced name must target an environment selected by the component.");
        }

        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 90)
        {
            return Error.Validation("RESOURCE_FORCED_NAME_INVALID", "A forced resource name must contain between 1 and 90 characters.");
        }

        var existing = forcedNames.SingleOrDefault(item => item.TargetId == environmentId);
        if (existing is null)
        {
            forcedNames.Add(new ResourceNameOverride(environmentId, normalized));
        }
        else
        {
            existing.Set(normalized);
        }

        Touch();
        return Result.Success;
    }

    internal ErrorOr<ResourceChild> AddChild(
        Guid id,
        string type,
        string name,
        IReadOnlyDictionary<string, JsonElement> properties)
    {
        if (children.Any(child => string.Equals(child.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("RESOURCE_CHILD_NAME_DUPLICATE", "A child with this name already exists in its resource.");
        }

        var created = ResourceChild.Create(id, type, name, properties);
        if (created.IsError)
        {
            return created.Errors;
        }

        children.Add(created.Value);
        Touch();
        return created.Value;
    }

    internal ErrorOr<Success> UpdateChild(Guid id, string name, IReadOnlyDictionary<string, JsonElement> properties)
    {
        var child = children.SingleOrDefault(candidate => candidate.Id == id);
        if (child is null)
        {
            return Error.NotFound("RESOURCE_CHILD_NOT_FOUND", "The child was not found.");
        }

        if (children.Any(candidate => candidate.Id != id
            && string.Equals(candidate.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("RESOURCE_CHILD_NAME_DUPLICATE", "A child with this name already exists in its resource.");
        }

        var updated = child.Update(name, properties);
        if (updated.IsError)
        {
            return updated.Errors;
        }

        Touch();
        return Result.Success;
    }

    internal ErrorOr<Success> RemoveChild(Guid id)
    {
        var child = children.SingleOrDefault(candidate => candidate.Id == id);
        if (child is null)
        {
            return Error.NotFound("RESOURCE_CHILD_NOT_FOUND", "The child was not found.");
        }

        children.Remove(child);
        Touch();
        return Result.Success;
    }

    internal ErrorOr<Success> MoveTo(Guid resourceGroupId)
    {
        if (resourceGroupId == Guid.Empty)
        {
            return Error.Validation("RESOURCE_GROUP_REQUIRED", "A resource group is required.");
        }

        ResourceGroupId = resourceGroupId;
        Touch();
        return Result.Success;
    }

    internal void SynchronizeTargets(IEnumerable<Guid> targetEnvironmentIds)
    {
        var targetIds = targetEnvironmentIds.ToHashSet();
        var changed = false;
        foreach (var environmentId in presences.Select(presence => presence.TargetId).ToArray())
        {
            if (targetIds.Contains(environmentId))
            {
                continue;
            }

            presences.RemoveAll(presence => presence.TargetId == environmentId);
            existingIds.RemoveAll(existingId => existingId.TargetId == environmentId);
            forcedNames.RemoveAll(forcedName => forcedName.TargetId == environmentId);
            RemoveEnvironmentValues(environmentId);
            changed = true;
        }

        foreach (var environmentId in targetIds)
        {
            if (!presences.Any(presence => presence.TargetId == environmentId))
            {
                presences.Add(new ResourcePresence(environmentId, !IsExisting));
                changed = true;
            }
        }

        if (changed)
        {
            Touch();
        }
    }

    internal bool AddTarget(Guid environmentId)
    {
        if (environmentId == Guid.Empty || presences.Any(presence => presence.TargetId == environmentId))
        {
            return false;
        }

        presences.Add(new ResourcePresence(environmentId, !IsExisting));
        Touch();
        return true;
    }

    internal bool RemoveTarget(Guid environmentId)
    {
        var removed = presences.RemoveAll(presence => presence.TargetId == environmentId) > 0;
        removed |= existingIds.RemoveAll(item => item.TargetId == environmentId) > 0;
        removed |= forcedNames.RemoveAll(item => item.TargetId == environmentId) > 0;
        var before = values.Count;
        RemoveEnvironmentValues(environmentId);
        removed |= values.Count != before;
        if (removed)
        {
            Touch();
        }

        return removed;
    }

    internal ErrorOr<Success> SetTags(IEnumerable<KeyValuePair<string, string>> values)
    {
        var normalized = NormalizeTags(values);
        if (normalized.IsError)
        {
            return normalized.Errors;
        }

        tags.Clear();
        tags.AddRange(normalized.Value.Select(tag => new ResourceTag(tag.Key, tag.Value)));
        Touch();
        return Result.Success;
    }

    internal ErrorOr<Success> SetMetadata(string description, IEnumerable<KeyValuePair<string, string>> values)
    {
        var normalizedDescription = description?.Trim() ?? string.Empty;
        if (normalizedDescription.Length > 500)
        {
            return Error.Validation("RESOURCE_DESCRIPTION_INVALID", "A resource description cannot exceed 500 characters.");
        }

        var normalizedTags = NormalizeTags(values);
        if (normalizedTags.IsError)
        {
            return normalizedTags.Errors;
        }

        Description = normalizedDescription;
        tags.Clear();
        tags.AddRange(normalizedTags.Value.Select(tag => new ResourceTag(tag.Key, tag.Value)));
        Touch();
        return Result.Success;
    }

    internal ErrorOr<Success> SetDefaultIdentity(Guid? identityResourceId)
    {
        if (identityResourceId == Guid.Empty)
        {
            return Error.Validation("VAL-LIA-IDENTITE", "A default user-assigned identity must name a resource in the same component.");
        }

        DefaultIdentityResourceId = identityResourceId;
        Touch();
        return Result.Success;
    }

    private static ErrorOr<IReadOnlyList<KeyValuePair<string, string>>> NormalizeTags(
        IEnumerable<KeyValuePair<string, string>>? values)
    {
        if (values is null)
        {
            return Error.Validation("RESOURCE_TAG_FORMAT", "Resource tags must be provided as a collection.");
        }

        var normalized = new List<KeyValuePair<string, string>>();
        foreach (var tag in values)
        {
            if (tag.Key is null || tag.Value is null)
            {
                return Error.Validation("RESOURCE_TAG_FORMAT", "Resource tag keys and values cannot be null.");
            }

            normalized.Add(new KeyValuePair<string, string>(tag.Key.Trim(), tag.Value.Trim()));
        }

        if (normalized.Count > 50
            || normalized.Any(tag => tag.Key.Length is < 1 or > 512
                || tag.Key.IndexOfAny(['<', '>', '%', '&', '\\', '?', '/']) >= 0
                || tag.Value.Length > 256))
        {
            return Error.Validation("RESOURCE_TAG_FORMAT", "Resource tags must have valid keys of at most 512 characters and values of at most 256 characters.");
        }

        if (normalized.Any(tag => ReservedTagKeys.Contains(tag.Key)))
        {
            return Error.Validation("RG-PRJ-06-SYSTEM-TAG-RESERVED", "System tag keys cannot be declared by a user.");
        }

        if (normalized.Select(tag => tag.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Count)
        {
            return Error.Validation("RESOURCE_TAG_DUPLICATE", "Resource tag keys must be unique without regard to case.");
        }

        return normalized;
    }

    public IReadOnlyDictionary<string, JsonElement> GetProperties(Guid? environmentId = null)
    {
        var roots = values.Where(value => value.ParentId is null && value.TargetId == environmentId);
        return roots.ToDictionary(
            root => root.PropertyName!,
            root => ResourceValueNode.ToJsonElement(values, root.Id),
            StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, JsonElement>> GetOverrides() =>
        values.Where(value => value.ParentId is null && value.TargetId is not null)
            .Select(value => value.TargetId!.Value)
            .Distinct()
            .ToDictionary(
                environmentId => environmentId,
                environmentId => GetProperties(environmentId));

    internal void Touch() => Version = checked(Version + 1);

    private void ReplaceValue(Guid? environmentId, string propertyName, JsonElement value)
    {
        var replacement = ResourceValueNode.CreateTree(propertyName, value, environmentId);
        RemoveTree(environmentId, propertyName);
        values.AddRange(replacement);
    }

    private void RemoveTree(Guid? environmentId, string propertyName)
    {
        var roots = values.Where(node => node.ParentId is null
                && node.TargetId == environmentId
                && string.Equals(node.PropertyName, propertyName, StringComparison.Ordinal))
            .Select(node => node.Id)
            .ToHashSet();
        if (roots.Count == 0)
        {
            return;
        }

        var allIds = values.ToDictionary(node => node.Id, node => node.ParentId);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var pair in allIds)
            {
                if (pair.Value is { } parentId && roots.Contains(parentId) && roots.Add(pair.Key))
                {
                    changed = true;
                }
            }
        }

        values.RemoveAll(node => roots.Contains(node.Id));
    }

    private void RemoveEnvironmentValues(Guid environmentId)
    {
        var roots = values.Where(node => node.ParentId is null && node.TargetId == environmentId)
            .Select(node => node.Id)
            .ToHashSet();
        if (roots.Count == 0)
        {
            return;
        }

        var allIds = values.ToDictionary(node => node.Id, node => node.ParentId);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var pair in allIds)
            {
                if (pair.Value is { } parentId && roots.Contains(parentId) && roots.Add(pair.Key))
                {
                    changed = true;
                }
            }
        }

        values.RemoveAll(node => roots.Contains(node.Id));
    }

[GeneratedRegex("^[a-z][a-z0-9-]{0,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex LogicalNamePattern();
}
