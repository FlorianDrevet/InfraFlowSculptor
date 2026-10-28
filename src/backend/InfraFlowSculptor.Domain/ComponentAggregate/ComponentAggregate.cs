using System.Text.RegularExpressions;
using System.Text.Json;
using ErrorOr;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Domain.ComponentAggregate;

[AuditedObject(ObjectType.Component, nameof(ComponentAggregate.Id), nameof(ComponentAggregate.OrganizationId))]
public sealed partial class ComponentAggregate : AggregateRoot<ComponentId>, IOrganizationOwned, IHasVersion
{
    private static readonly HashSet<string> ReservedTagKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ifs-project", "ifs-component", "ifs-environment", "managed-by"
    };

    private Guid[] targetEnvironmentIds = [];
    private readonly List<ComponentTag> tags = [];
    private readonly List<ResourceGroup> resourceGroups = [];
    private readonly List<Resource> resources = [];
    private readonly List<ResourceTypeNamingOverride> namingOverrides = [];

    public ProjectId ProjectId { get; private set; }

    public OrganizationId OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string NameKey { get; private set; } = string.Empty;

    public string CodeKey { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string? DefaultNameTemplate { get; private set; }

    public ComponentDeploymentMode Mode { get; private set; }

    public bool TargetsAllEnvironments { get; private set; }

    public Guid[] TargetEnvironmentIds
    {
        get => targetEnvironmentIds;
        private set => targetEnvironmentIds = value;
    }

    public IReadOnlyCollection<ResourceGroup> ResourceGroups => resourceGroups.AsReadOnly();

    public IReadOnlyCollection<Resource> Resources => resources.AsReadOnly();

    public IReadOnlyCollection<ComponentTag> Tags => tags.AsReadOnly();

    public IReadOnlyCollection<ResourceTypeNamingOverride> NamingOverrides => namingOverrides.AsReadOnly();

    public ComponentSingleTarget? SingleTarget { get; private set; }

    public RemovedResourceBehavior RemovedResourceBehavior { get; private set; }

    public bool DeletionProtectionEnabled { get; private set; }

    public int Version { get; private set; } = 1;

    public DateTimeOffset? FirstPublishedAt { get; private set; }

    private ComponentAggregate()
    {
    }

    private ComponentAggregate(
        ComponentId id,
        ProjectId projectId,
        OrganizationId organizationId,
        string name,
        string code,
        string nameKey,
        string codeKey,
        string description,
        ComponentDeploymentMode mode,
        bool targetsAllEnvironments,
        Guid[] targetEnvironmentIds,
        ComponentSingleTarget? singleTarget,
        RemovedResourceBehavior removedResourceBehavior,
        bool deletionProtectionEnabled) : base(id)
    {
        ProjectId = projectId;
        OrganizationId = organizationId;
        Name = name;
        Code = code;
        NameKey = nameKey;
        CodeKey = codeKey;
        Description = description;
        Mode = mode;
        TargetsAllEnvironments = targetsAllEnvironments;
        this.targetEnvironmentIds = targetEnvironmentIds;
        SingleTarget = singleTarget;
        RemovedResourceBehavior = removedResourceBehavior;
        DeletionProtectionEnabled = deletionProtectionEnabled;
    }

    public static ErrorOr<ComponentAggregate> Create(
        ComponentId id,
        ProjectId projectId,
        OrganizationId organizationId,
        string name,
        string code,
        string description,
        ComponentDeploymentMode mode,
        IEnumerable<ProjectEnvironment> projectEnvironments,
        bool targetsAllEnvironments = true,
        IEnumerable<Guid>? targetEnvironmentIds = null,
        ComponentSingleTarget? singleTarget = null,
        RemovedResourceBehavior removedResourceBehavior = RemovedResourceBehavior.Detach,
        bool deletionProtectionEnabled = true,
        IEnumerable<ComponentAggregate>? existingComponents = null)
    {
        if (id.Value == Guid.Empty || projectId.Value == Guid.Empty || organizationId.Value == Guid.Empty)
        {
            return Error.Validation("COMPONENT_IDENTITY_REQUIRED", "A component, project, and organization are required.");
        }

        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedCode = code?.Trim() ?? string.Empty;
        var normalizedDescription = description?.Trim() ?? string.Empty;
        if (normalizedName.Length is < 1 or > 60)
        {
            return Error.Validation("COMPONENT_NAME_INVALID", "A component name must contain between 1 and 60 characters.");
        }

        if (!CodePattern().IsMatch(normalizedCode))
        {
            return Error.Validation("COMPONENT_CODE_INVALID", "A component code must match ^[a-z][a-z0-9-]{1,19}$ and cannot end with a dash.");
        }

        if (normalizedDescription.Length > 1000)
        {
            return Error.Validation("COMPONENT_DESCRIPTION_INVALID", "A component description cannot exceed 1000 characters.");
        }

        if (!Enum.IsDefined(mode))
        {
            return Error.Validation("COMPONENT_MODE_INVALID", "The deployment mode must be PerEnvironment or Single.");
        }

        if (!Enum.IsDefined(removedResourceBehavior))
        {
            return Error.Validation("COMPONENT_REMOVED_RESOURCE_BEHAVIOR_INVALID", "Removed resource behavior must be Detach or Delete.");
        }

        var existing = (existingComponents ?? []).Where(component => component.ProjectId == projectId).ToArray();
        if (existing.Any(component => string.Equals(component.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("COMPONENT_NAME_DUPLICATE", "A component with this name already exists in the project.");
        }

        if (existing.Any(component => string.Equals(component.Code, normalizedCode, StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("COMPONENT_CODE_DUPLICATE", "A component with this code already exists in the project.");
        }

        var environments = projectEnvironments.ToArray();
        var environmentIds = environments.Select(environment => environment.Id).ToHashSet();
        var selectedEnvironmentIds = (targetEnvironmentIds ?? []).Distinct().ToArray();
        if (mode == ComponentDeploymentMode.PerEnvironment)
        {
            if (singleTarget is not null)
            {
                return Error.Validation("COMPONENT_SINGLE_TARGET_NOT_ALLOWED", "PerEnvironment components cannot have a Single target.");
            }

            if (targetsAllEnvironments && selectedEnvironmentIds.Length > 0)
            {
                return Error.Validation("COMPONENT_TARGET_SELECTION_INVALID", "A component targeting all environments cannot also provide a selected list.");
            }

            if (!targetsAllEnvironments && selectedEnvironmentIds.Length == 0)
            {
                return Error.Validation("COMPONENT_TARGET_REQUIRED", "Select at least one environment or target all environments.");
            }

            if (selectedEnvironmentIds.Any(environmentId => !environmentIds.Contains(environmentId)))
            {
                return Error.Validation("COMPONENT_TARGET_ENVIRONMENT_NOT_FOUND", "Every selected environment must belong to the project.");
            }
        }
        else
        {
            if (targetsAllEnvironments || selectedEnvironmentIds.Length > 0)
            {
                return Error.Validation("COMPONENT_SINGLE_TARGET_SELECTION_INVALID", "Single components use their own target, not project environments.");
            }

            if (singleTarget is null)
            {
                return Error.Validation("COMPONENT_SINGLE_TARGET_REQUIRED", "A Single component requires its own target.");
            }

            if (environments.Any(environment => string.Equals(environment.Code, singleTarget.Code, StringComparison.OrdinalIgnoreCase)))
            {
                return Error.Conflict(
                    "COMPONENT_SINGLE_TARGET_ENVIRONMENT_CODE_CONFLICT",
                    "The Single target code must differ from every project environment code.");
            }
        }

        return new ComponentAggregate(
            id,
            projectId,
            organizationId,
            normalizedName,
            normalizedCode,
            normalizedName.ToUpperInvariant(),
            normalizedCode.ToUpperInvariant(),
            normalizedDescription,
            mode,
            targetsAllEnvironments,
            selectedEnvironmentIds,
            singleTarget,
            removedResourceBehavior,
            deletionProtectionEnabled);
    }

    public ErrorOr<Success> Update(
        string name,
        string code,
        string description,
        ComponentDeploymentMode mode,
        IEnumerable<ProjectEnvironment> projectEnvironments,
        bool targetsAllEnvironments,
        IEnumerable<Guid>? selectedEnvironmentIds,
        ComponentSingleTarget? singleTarget,
        RemovedResourceBehavior removedResourceBehavior,
        bool deletionProtectionEnabled,
        IEnumerable<ComponentAggregate>? existingComponents = null)
    {
        var normalizedCode = code?.Trim() ?? string.Empty;
        if (FirstPublishedAt is not null
            && (!string.Equals(Code, normalizedCode, StringComparison.Ordinal)
                || Mode != mode))
        {
            return Error.Conflict("COMPONENT_IDENTITY_LOCKED", "The component code and deployment mode are locked after publication.");
        }

        var candidate = Create(
            Id,
            ProjectId,
            OrganizationId,
            name,
            normalizedCode,
            description,
            mode,
            projectEnvironments,
            targetsAllEnvironments,
            selectedEnvironmentIds,
            singleTarget,
            removedResourceBehavior,
            deletionProtectionEnabled,
            (existingComponents ?? []).Where(component => component.Id != Id));
        if (candidate.IsError)
        {
            return candidate.Errors;
        }

        if (SingleTarget is { } currentTarget && candidate.Value.SingleTarget is { } updatedTarget)
        {
            updatedTarget.PreserveIdentity(currentTarget.Id);
        }

        Name = candidate.Value.Name;
        Code = candidate.Value.Code;
        NameKey = candidate.Value.NameKey;
        CodeKey = candidate.Value.CodeKey;
        Description = candidate.Value.Description;
        Mode = candidate.Value.Mode;
        TargetsAllEnvironments = candidate.Value.TargetsAllEnvironments;
        targetEnvironmentIds = candidate.Value.targetEnvironmentIds;
        SingleTarget = candidate.Value.SingleTarget;
        RemovedResourceBehavior = candidate.Value.RemovedResourceBehavior;
        DeletionProtectionEnabled = candidate.Value.DeletionProtectionEnabled;
        SynchronizeResourceTargets(projectEnvironments);
        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> ConfigureDefaultNameTemplate(string? template)
    {
        var normalizedTemplate = template?.Trim();
        if (normalizedTemplate is not null && normalizedTemplate.Length is < 1 or > 200)
        {
            return Error.Validation("PROJECT_NAMING_TEMPLATE_INVALID", "A naming template must contain between 1 and 200 characters.");
        }

        if (FirstPublishedAt is not null
            && !string.Equals(DefaultNameTemplate, normalizedTemplate, StringComparison.Ordinal))
        {
            return Error.Conflict("PROJECT_NAMING_LOCKED_AFTER_PUBLICATION", "Naming settings cannot change after publication until the impact preview is available.");
        }

        DefaultNameTemplate = normalizedTemplate;
        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> ConfigureTypeNamingOverride(string type, string? template, string? abbreviation)
    {
        var candidate = ResourceTypeNamingOverride.Create(type, template, abbreviation);
        if (candidate.IsError)
        {
            return candidate.Errors;
        }

        var existing = namingOverrides.SingleOrDefault(value =>
            string.Equals(value.Type, candidate.Value.Type, StringComparison.OrdinalIgnoreCase));
        if (FirstPublishedAt is not null
            && (existing is null
                || !string.Equals(existing.Template, candidate.Value.Template, StringComparison.Ordinal)
                || !string.Equals(existing.Abbreviation, candidate.Value.Abbreviation, StringComparison.Ordinal)))
        {
            return Error.Conflict("PROJECT_NAMING_LOCKED_AFTER_PUBLICATION", "Naming settings cannot change after publication until the impact preview is available.");
        }

        if (existing is not null && candidate.Value.Template is null && candidate.Value.Abbreviation is null)
        {
            namingOverrides.Remove(existing);
        }
        else if (existing is not null)
        {
            existing.SetValues(candidate.Value.Template, candidate.Value.Abbreviation);
        }
        else if (candidate.Value.Template is not null || candidate.Value.Abbreviation is not null)
        {
            namingOverrides.Add(candidate.Value);
        }

        Touch();
        return Result.Success;
    }

    public ErrorOr<ResourceGroup> AddResourceGroup(
        Guid id,
        string name,
        string? region = null,
        IEnumerable<KeyValuePair<string, string>>? tags = null,
        IEnumerable<KeyValuePair<Guid, string>>? forcedNames = null)
    {
        if (resourceGroups.Any(group => string.Equals(group.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("RESOURCE_GROUP_NAME_DUPLICATE", "A resource group with this name already exists in the component.");
        }

        var created = ResourceGroup.Create(id, ProjectId, OrganizationId, name, region, tags, forcedNames);
        if (created.IsError)
        {
            return created.Errors;
        }

        resourceGroups.Add(created.Value);
        Touch();
        return created.Value;
    }

    public ErrorOr<Success> UpdateResourceGroup(
        Guid id,
        string name,
        string? region,
        IEnumerable<KeyValuePair<string, string>>? tags = null,
        IEnumerable<KeyValuePair<Guid, string>>? forcedNames = null)
    {
        var group = resourceGroups.SingleOrDefault(candidate => candidate.Id == id);
        if (group is null)
        {
            return Error.NotFound("RESOURCE_GROUP_NOT_FOUND", "The resource group was not found.");
        }

        if (resourceGroups.Any(candidate => candidate.Id != id
            && string.Equals(candidate.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("RESOURCE_GROUP_NAME_DUPLICATE", "A resource group with this name already exists in the component.");
        }

        var updated = group.Update(name, region, tags, forcedNames);
        if (updated.IsError)
        {
            return updated.Errors;
        }

        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> RemoveResourceGroup(Guid id, bool containsResources)
    {
        var group = resourceGroups.SingleOrDefault(candidate => candidate.Id == id);
        if (group is null)
        {
            return Error.NotFound("RESOURCE_GROUP_NOT_FOUND", "The resource group was not found.");
        }

        if (containsResources)
        {
            return Error.Conflict("RESOURCE_GROUP_NOT_EMPTY", "A resource group cannot be deleted while it contains resources.");
        }

        resourceGroups.Remove(group);
        Touch();
        return Result.Success;
    }

    public ErrorOr<Resource> AddResource(
        Guid id,
        Guid resourceGroupId,
        string type,
        string logicalName,
        string? description,
        bool isExisting,
        IEnumerable<Guid> targetEnvironmentIds,
        IReadOnlyDictionary<string, JsonElement> properties,
        IEnumerable<KeyValuePair<string, string>>? tags = null)
    {
        if (!resourceGroups.Any(group => group.Id == resourceGroupId))
        {
            return Error.NotFound("RESOURCE_GROUP_NOT_FOUND", "The resource group was not found.");
        }

        if (resources.Any(resource => string.Equals(resource.Type, type?.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(resource.LogicalName, logicalName?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("RESOURCE_LOGICAL_NAME_DUPLICATE", "A resource with this logical name and type already exists in the component.");
        }

        var created = Resource.Create(
            id,
            ProjectId,
            OrganizationId,
            resourceGroupId,
            type,
            logicalName,
            description,
            isExisting,
            targetEnvironmentIds,
            properties,
            tags);
        if (created.IsError)
        {
            return created.Errors;
        }

        resources.Add(created.Value);
        Touch();
        return created.Value;
    }

    public ErrorOr<Success> SetResourceProperties(Guid resourceId, IReadOnlyDictionary<string, JsonElement> properties)
    {
        var resource = FindResource(resourceId);
        if (resource is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        var updated = resource.SetProperties(properties);
        if (updated.IsError)
        {
            return updated.Errors;
        }

        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> UpdateResourceMetadata(
        Guid resourceId,
        string description,
        IEnumerable<KeyValuePair<string, string>> tags) =>
        UpdateResource(resourceId, resource => resource.SetMetadata(description, tags));

    public ErrorOr<Success> SetResourceOverride(Guid resourceId, Guid targetId, string propertyName, JsonElement value) =>
        UpdateResource(resourceId, resource => resource.SetOverride(targetId, propertyName, value));

    public ErrorOr<Success> ClearResourceOverride(Guid resourceId, Guid targetId, string propertyName) =>
        UpdateResource(resourceId, resource => resource.ClearOverride(targetId, propertyName));

    public ErrorOr<Success> SetResourcePresence(Guid resourceId, Guid targetId, bool isPresent) =>
        UpdateResource(resourceId, resource => resource.SetPresence(targetId, isPresent));

    public ErrorOr<Success> SetExistingResourceId(Guid resourceId, Guid targetId, string value) =>
        UpdateResource(resourceId, resource => resource.SetExistingResourceId(targetId, value));

    public ErrorOr<Success> SetResourceForcedName(Guid resourceId, Guid targetId, string name)
    {
        var resource = FindResource(resourceId);
        var currentName = resource?.ForcedNames.SingleOrDefault(item => item.TargetId == targetId)?.Name;
        if (FirstPublishedAt is not null
            && resource is not null
            && !string.Equals(currentName, name?.Trim(), StringComparison.Ordinal))
        {
            return Error.Conflict(
                "PROJECT_NAMING_LOCKED_AFTER_PUBLICATION",
                "Naming settings cannot change after publication until the impact preview is available.");
        }

        return UpdateResource(resourceId, candidate => candidate.SetForcedName(targetId, name ?? string.Empty));
    }

    public ErrorOr<ResourceChild> AddResourceChild(
        Guid resourceId,
        Guid childId,
        string type,
        string name,
        IReadOnlyDictionary<string, JsonElement> properties)
    {
        var resource = FindResource(resourceId);
        if (resource is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        var created = resource.AddChild(childId, type, name, properties);
        if (created.IsError)
        {
            return created.Errors;
        }

        Touch();
        return created.Value;
    }

    public ErrorOr<Success> UpdateResourceChild(
        Guid resourceId,
        Guid childId,
        string name,
        IReadOnlyDictionary<string, JsonElement> properties)
    {
        var resource = FindResource(resourceId);
        if (resource is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        var updated = resource.UpdateChild(childId, name, properties);
        if (updated.IsError)
        {
            return updated.Errors;
        }

        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> RemoveResourceChild(Guid resourceId, Guid childId)
    {
        var resource = FindResource(resourceId);
        if (resource is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        var removed = resource.RemoveChild(childId);
        if (removed.IsError)
        {
            return removed.Errors;
        }

        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> MoveResource(Guid resourceId, Guid resourceGroupId)
    {
        var resource = FindResource(resourceId);
        if (resource is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        if (!resourceGroups.Any(group => group.Id == resourceGroupId))
        {
            return Error.NotFound("RESOURCE_GROUP_NOT_FOUND", "The resource group was not found.");
        }

        var moved = resource.MoveTo(resourceGroupId);
        if (moved.IsError)
        {
            return moved.Errors;
        }

        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> RemoveResource(Guid resourceId)
    {
        var resource = FindResource(resourceId);
        if (resource is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        resources.Remove(resource);
        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> SetTags(IEnumerable<KeyValuePair<string, string>> values)
    {
        var normalized = values
            .Select(tag => new KeyValuePair<string, string>(tag.Key.Trim(), tag.Value.Trim()))
            .ToArray();
        if (normalized.Length > 50)
        {
            return Error.Validation("COMPONENT_TAG_LIMIT", "A maximum of 50 tags may be declared.");
        }

        if (normalized.Any(tag => tag.Key.Length is < 1 or > 512
            || tag.Key.IndexOfAny(['<', '>', '%', '&', '\\', '?', '/']) >= 0
            || tag.Value.Length > 256))
        {
            return Error.Validation("COMPONENT_TAG_FORMAT", "Tag keys must be 1-512 valid characters and values cannot exceed 256 characters.");
        }

        if (normalized.Any(tag => ReservedTagKeys.Contains(tag.Key)))
        {
            return Error.Validation("RG-PRJ-06-SYSTEM-TAG-RESERVED", "System tag keys cannot be declared by a user.");
        }

        if (normalized.Select(tag => tag.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
        {
            return Error.Validation("COMPONENT_TAG_DUPLICATE", "Tag keys must be unique without regard to case.");
        }

        tags.Clear();
        tags.AddRange(normalized.Select(tag => new ComponentTag(tag.Key, tag.Value)));
        Touch();
        return Result.Success;
    }

    public bool TargetsEnvironment(Guid environmentId) =>
        Mode == ComponentDeploymentMode.PerEnvironment
        && (TargetsAllEnvironments || targetEnvironmentIds.Contains(environmentId));

    public bool WouldLoseLastTargetAfterRemoving(Guid environmentId) =>
        Mode == ComponentDeploymentMode.PerEnvironment
        && !TargetsAllEnvironments
        && targetEnvironmentIds.Length == 1
        && targetEnvironmentIds.Contains(environmentId);

    public ErrorOr<Success> RemoveTargetEnvironment(Guid environmentId)
    {
        if (Mode != ComponentDeploymentMode.PerEnvironment || TargetsAllEnvironments)
        {
            return Result.Success;
        }

        if (!targetEnvironmentIds.Contains(environmentId))
        {
            return Result.Success;
        }

        if (WouldLoseLastTargetAfterRemoving(environmentId))
        {
            return Error.Conflict("COMPONENT_TARGET_REQUIRED", "A PerEnvironment component must keep at least one target environment.");
        }

        targetEnvironmentIds = targetEnvironmentIds.Where(id => id != environmentId).ToArray();
        RemoveResourceTargetEnvironment(environmentId);
        Touch();
        return Result.Success;
    }

    public bool AddResourceTargetEnvironment(Guid environmentId)
    {
        if (!TargetsEnvironment(environmentId))
        {
            return false;
        }

        var changed = false;
        foreach (var resource in resources)
        {
            changed |= resource.AddTarget(environmentId);
        }
        if (changed)
        {
            Touch();
        }

        return changed;
    }

    public bool RemoveResourceTargetEnvironment(Guid environmentId)
    {
        var changed = false;
        foreach (var resource in resources)
        {
            changed |= resource.RemoveTarget(environmentId);
        }

        if (changed)
        {
            Touch();
        }

        return changed;
    }

    public bool HasSingleTargetCode(string environmentCode) =>
        Mode == ComponentDeploymentMode.Single
        && SingleTarget is not null
        && string.Equals(SingleTarget.Code, environmentCode, StringComparison.OrdinalIgnoreCase);

    private Resource? FindResource(Guid id) => resources.SingleOrDefault(resource => resource.Id == id);

    private ErrorOr<Success> UpdateResource(Guid id, Func<Resource, ErrorOr<Success>> update)
    {
        var resource = FindResource(id);
        if (resource is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        var result = update(resource);
        if (result.IsError)
        {
            return result.Errors;
        }

        Touch();
        return Result.Success;
    }

    private void SynchronizeResourceTargets(IEnumerable<ProjectEnvironment> projectEnvironments)
    {
        var targetIds = Mode == ComponentDeploymentMode.PerEnvironment
            ? projectEnvironments.Where(environment => TargetsEnvironment(environment.Id))
                .Select(environment => environment.Id)
                .ToArray()
            : SingleTarget is { } target ? [target.Id] : [];
        foreach (var resource in resources)
        {
            resource.SynchronizeTargets(targetIds);
        }
    }

    public ErrorOr<Success> EnsureCanBeDeleted() => FirstPublishedAt is null
        ? Result.Success
        : Error.Conflict("COMPONENT_DELETE_PUBLISHED", "Only components that have never been published can be deleted in the pilot.");

    public void RecordPublication(DateTimeOffset publishedAt) => FirstPublishedAt ??= publishedAt;

    private void Touch() => Version = checked(Version + 1);

    [GeneratedRegex("^[a-z][a-z0-9-]{0,18}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
