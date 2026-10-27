using System.Text.RegularExpressions;
using ErrorOr;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Domain.ProjectAggregate;

[AuditedObject(ObjectType.Project, nameof(ProjectAggregate.Id), nameof(ProjectAggregate.OrganizationId))]
public sealed partial class ProjectAggregate : AggregateRoot<ProjectId>, IOrganizationOwned, IHasVersion
{
    private static readonly HashSet<string> ReservedTagKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ifs-project", "ifs-component", "ifs-environment", "managed-by"
    };

    private readonly List<ProjectRoleAssignment> roleAssignments = [];
    private readonly List<ProjectEnvironment> environments = [];
    private readonly List<ProjectTag> tags = [];

    public OrganizationId OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public int Version { get; private set; } = 1;

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid TenantId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public string InfrastructureLanguage { get; private set; } = "Bicep";

    public string CiPlatform { get; private set; } = "AzureDevOps";

    public string ModuleSource { get; private set; } = "AVM registre public";

    public string CatalogVersion { get; private set; } = string.Empty;

    public string NamingPreset { get; private set; } = ProjectNamingPresets.Compact;

    public string DefaultNameTemplate { get; private set; } = ProjectNamingPresets.CompactDefaultTemplate;

    public string ResourceGroupNameTemplate { get; private set; } = ProjectNamingPresets.CompactResourceGroupTemplate;

    public string? PublishingPreset { get; private set; }

    public bool SystemTagsEnabled { get; private set; } = true;

    public int ModelVersion { get; private set; }

    public DateTimeOffset? FirstPublishedAt { get; private set; }

    public UserProfileId? CoordinatorUserProfileId { get; private set; }

    public string[] DefaultRunners { get; private set; } = [];

    public string[] DefaultReviewers { get; private set; } = [];

    public IReadOnlyCollection<ProjectEnvironment> Environments => environments.AsReadOnly();

    public IReadOnlyCollection<ProjectTag> Tags => tags.AsReadOnly();

    public IReadOnlyCollection<ProjectRoleAssignment> RoleAssignments => roleAssignments.AsReadOnly();

    private ProjectAggregate()
    {
    }

    private ProjectAggregate(
        ProjectId id,
        OrganizationId organizationId,
        string name,
        string code,
        Guid tenantId = default,
        string description = "",
        string catalogVersion = "") : base(id)
    {
        OrganizationId = organizationId;
        Name = name;
        Code = code;
        TenantId = tenantId;
        Description = description;
        CatalogVersion = catalogVersion;
    }

    public static ErrorOr<ProjectAggregate> Create(
        ProjectId id,
        OrganizationId organizationId,
        string name,
        string code,
        UserProfileId creatorProfileId)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedCode = code?.Trim() ?? string.Empty;
        if (id.Value == Guid.Empty
            || organizationId.Value == Guid.Empty
            || creatorProfileId.Value == Guid.Empty)
        {
            return Error.Validation("PROJECT_IDENTITY_REQUIRED", "A project, organization, name, code, and creator are required.");
        }

        if (normalizedName.Length is < 3 or > 80)
        {
            return Error.Validation("PROJECT_NAME_INVALID", "A project name must contain between 3 and 80 characters.");
        }

        if (!ProjectCodePattern().IsMatch(normalizedCode))
        {
            return Error.Validation("PROJECT_CODE_INVALID", "A project code must match ^[a-z][a-z0-9]{1,9}$.");
        }

        var project = new ProjectAggregate(id, organizationId, normalizedName, normalizedCode);
        project.roleAssignments.Add(new ProjectRoleAssignment(
            organizationId,
            creatorProfileId,
            ProjectRole.Owner,
            ProjectRoleScope.Project));
        return project;
    }

    public static ErrorOr<ProjectAggregate> Create(
        ProjectId id,
        OrganizationId organizationId,
        string name,
        string code,
        Guid tenantId,
        string description,
        string catalogVersion,
        UserProfileId creatorProfileId,
        UserProfileId? coordinatorUserProfileId = null,
        IEnumerable<string>? defaultRunners = null,
        IEnumerable<string>? defaultReviewers = null)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(catalogVersion))
        {
            return Error.Validation("PROJECT_TENANT_AND_CATALOG_REQUIRED", "A project tenant and pinned catalog version are required.");
        }

        var normalizedDescription = description?.Trim() ?? string.Empty;
        if (normalizedDescription.Length > 1000)
        {
            return Error.Validation("PROJECT_DESCRIPTION_INVALID", "A project description cannot exceed 1000 characters.");
        }

        var project = Create(id, organizationId, name, code, creatorProfileId);
        if (project.IsError)
        {
            return project.Errors;
        }

        project.Value.TenantId = tenantId;
        project.Value.Description = normalizedDescription;
        project.Value.CatalogVersion = catalogVersion.Trim();
        project.Value.CoordinatorUserProfileId = coordinatorUserProfileId;
        project.Value.DefaultRunners = NormalizeValues(defaultRunners);
        project.Value.DefaultReviewers = NormalizeValues(defaultReviewers);
        return project.Value;
    }

    public ErrorOr<ProjectEnvironment> AddEnvironment(
        Guid id,
        string name,
        string code,
        Guid subscriptionId,
        string region,
        int position,
        bool isProtected,
        IEnumerable<string> approvers,
        IEnumerable<KeyValuePair<string, string>> environmentTags,
        string description,
        IEnumerable<string>? runners = null,
        string? deploymentConnection = null)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedCode = code?.Trim() ?? string.Empty;
        if (environments.Any(environment => string.Equals(environment.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("ENVIRONMENT_NAME_DUPLICATE", "An environment with this name already exists in the project.");
        }

        if (environments.Any(environment => string.Equals(environment.Code, normalizedCode, StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("ENVIRONMENT_CODE_DUPLICATE", "An environment with this code already exists in the project.");
        }

        if (position < 1 || position > environments.Count + 1)
        {
            return Error.Validation("ENVIRONMENT_POSITION_INVALID", "The environment position must be between 1 and the end of the promotion chain.");
        }

        var boundedPosition = position;
        var connection = string.IsNullOrWhiteSpace(deploymentConnection)
            ? $"ifs-{Code}-{normalizedCode}"
            : deploymentConnection.Trim();
        var created = ProjectEnvironment.Create(
            id,
            normalizedName,
            normalizedCode,
            boundedPosition,
            subscriptionId,
            region,
            connection,
            isProtected,
            approvers,
            runners ?? [],
            description,
            environmentTags);
        if (created.IsError)
        {
            return created.Errors;
        }

        foreach (var environment in environments.Where(environment => environment.Position >= boundedPosition))
        {
            environment.SetPosition(environment.Position + 1);
        }

        environments.Add(created.Value);
        return created.Value;
    }

    public ErrorOr<Success> ChangeCode(string code)
    {
        var normalizedCode = code?.Trim() ?? string.Empty;
        if (!ProjectCodePattern().IsMatch(normalizedCode))
        {
            return Error.Validation("PROJECT_CODE_INVALID", "A project code must match ^[a-z][a-z0-9]{1,9}$.");
        }

        if (FirstPublishedAt is not null && !string.Equals(Code, normalizedCode, StringComparison.Ordinal))
        {
            return Error.Conflict("RG-PRJ-01-CODE-LOCKED", "The project code cannot change after its first publication.");
        }

        Code = normalizedCode;
        return Result.Success;
    }

    public ErrorOr<Success> UpdateEnvironment(
        Guid id,
        string name,
        string code,
        Guid subscriptionId,
        string region,
        bool isProtected,
        IEnumerable<string> approvers,
        IEnumerable<string> runners,
        string description,
        IEnumerable<KeyValuePair<string, string>> environmentTags,
        bool confirmImpact,
        string? deploymentConnection = null)
    {
        var environment = environments.SingleOrDefault(candidate => candidate.Id == id);
        if (environment is null)
        {
            return Error.NotFound("ENVIRONMENT_NOT_FOUND", "The environment was not found.");
        }

        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedCode = code?.Trim() ?? string.Empty;
        if (environments.Any(candidate => candidate.Id != id
            && string.Equals(candidate.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("ENVIRONMENT_NAME_DUPLICATE", "An environment with this name already exists in the project.");
        }

        if (environments.Any(candidate => candidate.Id != id
            && string.Equals(candidate.Code, normalizedCode, StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("ENVIRONMENT_CODE_DUPLICATE", "An environment with this code already exists in the project.");
        }

        var updated = environment.Update(
            normalizedName,
            normalizedCode,
            subscriptionId,
            region,
            isProtected,
            approvers,
            runners,
            description,
            environmentTags,
            confirmImpact,
            deploymentConnection);
        return updated;
    }

    public ErrorOr<Success> RemoveEnvironment(Guid id)
    {
        var environment = environments.SingleOrDefault(candidate => candidate.Id == id);
        if (environment is null)
        {
            return Error.NotFound("ENVIRONMENT_NOT_FOUND", "The environment was not found.");
        }

        environments.Remove(environment);
        foreach (var remaining in environments.Where(candidate => candidate.Position > environment.Position))
        {
            remaining.SetPosition(remaining.Position - 1);
        }

        return Result.Success;
    }

    public ErrorOr<Success> ReorderEnvironment(Guid id, int position)
    {
        var environment = environments.SingleOrDefault(candidate => candidate.Id == id);
        if (environment is null)
        {
            return Error.NotFound("ENVIRONMENT_NOT_FOUND", "The environment was not found.");
        }

        if (position < 1 || position > environments.Count)
        {
            return Error.Validation("ENVIRONMENT_POSITION_INVALID", "The position must be between 1 and the end of the promotion chain.");
        }

        var boundedPosition = position;
        var oldPosition = environment.Position;
        foreach (var other in environments.Where(candidate => candidate.Id != id))
        {
            if (oldPosition < boundedPosition && other.Position > oldPosition && other.Position <= boundedPosition)
            {
                other.SetPosition(other.Position - 1);
            }
            else if (oldPosition > boundedPosition && other.Position >= boundedPosition && other.Position < oldPosition)
            {
                other.SetPosition(other.Position + 1);
            }
        }

        environment.SetPosition(boundedPosition);
        return Result.Success;
    }

    public ErrorOr<Success> SetTags(IEnumerable<KeyValuePair<string, string>> values)
    {
        var validated = ValidateTags(values);
        if (validated.IsError)
        {
            return validated.Errors;
        }

        ReplaceTags(tags, validated.Value);
        return Result.Success;
    }

    public ErrorOr<Success> ConfigureNaming(string preset, string defaultNameTemplate, string resourceGroupNameTemplate)
    {
        var normalizedPreset = preset?.Trim() ?? string.Empty;
        var normalizedDefaultTemplate = defaultNameTemplate?.Trim() ?? string.Empty;
        var normalizedResourceGroupTemplate = resourceGroupNameTemplate?.Trim() ?? string.Empty;
        if (!new[] { ProjectNamingPresets.Compact, ProjectNamingPresets.Caf, ProjectNamingPresets.Custom }
                .Contains(normalizedPreset, StringComparer.OrdinalIgnoreCase))
        {
            return Error.Validation("PROJECT_NAMING_PRESET_INVALID", "The naming preset must be Compact, CAF, or Custom.");
        }

        if (normalizedDefaultTemplate.Length is < 1 or > 200
            || normalizedResourceGroupTemplate.Length is < 1 or > 200)
        {
            return Error.Validation("PROJECT_NAMING_TEMPLATE_INVALID", "Naming templates must contain between 1 and 200 characters.");
        }

        NamingPreset = normalizedPreset;
        DefaultNameTemplate = normalizedDefaultTemplate;
        ResourceGroupNameTemplate = normalizedResourceGroupTemplate;
        return Result.Success;
    }

    public ErrorOr<Success> ConfigurePublishing(string? preset)
    {
        var normalizedPreset = preset?.Trim();
        if (normalizedPreset is not null
            && !string.Equals(normalizedPreset, "MonoRepo", StringComparison.Ordinal))
        {
            return Error.Validation("PROJECT_PUBLISHING_PRESET_INVALID", "Only the MonoRepo publishing preset is available in this plan step.");
        }

        PublishingPreset = normalizedPreset;
        return Result.Success;
    }

    public void RecordModelChange() => ModelVersion = checked(ModelVersion + 1);

    public void RecordPublication(DateTimeOffset publishedAt)
    {
        FirstPublishedAt ??= publishedAt;
        foreach (var environment in environments)
        {
            environment.RecordPublication(publishedAt);
        }
    }

    public ErrorOr<Success> SoftDelete(DateTimeOffset deletedAt)
    {
        if (DeletedAt is not null)
        {
            return Error.Conflict("PROJECT_ALREADY_DELETED", "The project is already deleted.");
        }

        DeletedAt = deletedAt;
        return Result.Success;
    }

    public ErrorOr<Success> Restore(DateTimeOffset now)
    {
        if (DeletedAt is null)
        {
            return Error.Conflict("PROJECT_NOT_DELETED", "The project is not deleted.");
        }

        if (now - DeletedAt.Value > TimeSpan.FromDays(30))
        {
            return Error.Conflict("PROJECT_RESTORE_EXPIRED", "The project can only be restored within 30 days of deletion.");
        }

        DeletedAt = null;
        return Result.Success;
    }

    internal static ErrorOr<IReadOnlyList<KeyValuePair<string, string>>> ValidateTags(
        IEnumerable<KeyValuePair<string, string>> values)
    {
        var normalized = values.Select(tag => new KeyValuePair<string, string>(tag.Key.Trim(), tag.Value.Trim())).ToArray();
        if (normalized.Length > 50)
        {
            return Error.Validation("RG-PRJ-07-TAG-LIMIT", "A maximum of 50 tags may be declared.");
        }

        if (normalized.Any(tag => tag.Key.Length is < 1 or > 512
            || tag.Key.IndexOfAny(['<', '>', '%', '&', '\\', '?', '/']) >= 0
            || tag.Value.Length > 256))
        {
            return Error.Validation("RG-PRJ-07-TAG-FORMAT", "Tag keys must be 1-512 valid characters and values cannot exceed 256 characters.");
        }

        if (normalized.Any(tag => ReservedTagKeys.Contains(tag.Key)))
        {
            return Error.Validation("RG-PRJ-06-SYSTEM-TAG-RESERVED", "System tag keys cannot be declared by a user.");
        }

        if (normalized.Select(tag => tag.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
        {
            return Error.Validation("PROJECT_TAG_KEY_DUPLICATE", "Tag keys must be unique without regard to case.");
        }

        return normalized;
    }

    private static string[] NormalizeValues(IEnumerable<string>? values) => (values ?? [])
        .Select(value => value?.Trim() ?? string.Empty)
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    internal static void ReplaceTags(
        List<ProjectTag> current,
        IEnumerable<KeyValuePair<string, string>> desiredTags)
    {
        var desired = desiredTags.ToArray();
        current.RemoveAll(tag => !desired.Any(candidate =>
            string.Equals(candidate.Key, tag.Key, StringComparison.OrdinalIgnoreCase)));
        foreach (var candidate in desired)
        {
            var existing = current.SingleOrDefault(tag =>
                string.Equals(candidate.Key, tag.Key, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                current.Add(new ProjectTag(candidate.Key, candidate.Value));
            }
            else
            {
                existing.SetValue(candidate.Value);
            }
        }
    }

    public ErrorOr<Success> AssignRole(
        UserProfileId principal,
        ProjectRole role,
        ProjectRoleScope scope = ProjectRoleScope.Project)
    {
        if (principal.Value == Guid.Empty)
        {
            return Error.Validation("PROJECT_ROLE_PRINCIPAL_REQUIRED", "A project role principal is required.");
        }

        if (!Enum.IsDefined(role) || !Enum.IsDefined(scope))
        {
            return Error.Validation("PROJECT_ROLE_INVALID", "The project role or scope is not supported.");
        }

        if (roleAssignments.Any(assignment =>
                assignment.Principal == principal && assignment.Role == role && assignment.Scope == scope))
        {
            return Error.Conflict("PROJECT_ROLE_ALREADY_ASSIGNED", "The project role is already assigned to this member.");
        }

        roleAssignments.Add(new ProjectRoleAssignment(OrganizationId, principal, role, scope));
        Version = checked(Version + 1);
        return Result.Success;
    }

    public ErrorOr<Success> RemoveRole(
        UserProfileId principal,
        ProjectRole role,
        ProjectRoleScope scope = ProjectRoleScope.Project)
    {
        var assignment = roleAssignments.SingleOrDefault(candidate =>
            candidate.Principal == principal && candidate.Role == role && candidate.Scope == scope);
        if (assignment is null)
        {
            return Error.NotFound("PROJECT_ROLE_NOT_FOUND", "The project role assignment was not found.");
        }

        if (role == ProjectRole.Owner && CountNamedOwners() == 1)
        {
            return Error.Conflict("RG-ORG-11", "A project must retain at least one named owner.");
        }

        roleAssignments.Remove(assignment);
        Version = checked(Version + 1);
        return Result.Success;
    }

    public ErrorOr<Success> RemoveAllRolesForMember(UserProfileId principal)
    {
        var assignments = roleAssignments.Where(assignment => assignment.Principal == principal).ToArray();
        if (assignments.Length == 0)
        {
            return Result.Success;
        }

        var ownedAssignments = assignments.Count(assignment => assignment.Role == ProjectRole.Owner);
        if (ownedAssignments > 0 && CountNamedOwners() <= ownedAssignments)
        {
            return Error.Conflict("RG-ORG-11", "A project must retain at least one named owner.");
        }

        roleAssignments.RemoveAll(assignment => assignment.Principal == principal);
        Version = checked(Version + 1);
        return Result.Success;
    }

    public ErrorOr<Success> TakeOwnership(UserProfileId principal)
    {
        if (HasRole(principal, ProjectRole.Owner))
        {
            return Result.Success;
        }

        return AssignRole(principal, ProjectRole.Owner);
    }

    public IReadOnlySet<string> EffectivePermissions(UserProfileId principal)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assignment in roleAssignments.Where(candidate => candidate.Principal == principal))
        {
            permissions.UnionWith(ProjectRoles.PermissionsFor(assignment.Role));
        }

        if (permissions.Count > 0)
        {
            permissions.Add(Permission.ProjectRead);
        }

        return new ReadOnlySet<string>(permissions);
    }

    public bool HasRole(UserProfileId principal, ProjectRole role) =>
        roleAssignments.Any(assignment => assignment.Principal == principal && assignment.Role == role);

    private int CountNamedOwners() =>
        roleAssignments.Count(assignment => assignment.Role == ProjectRole.Owner);

    [GeneratedRegex("^[a-z][a-z0-9]{1,9}$", RegexOptions.CultureInvariant)]
    private static partial Regex ProjectCodePattern();

    [GeneratedRegex("^[a-z][a-z0-9]{1,7}$", RegexOptions.CultureInvariant)]
    internal static partial Regex EnvironmentCodePattern();

    private sealed class ReadOnlySet<T>(HashSet<T> values) : IReadOnlySet<T>
    {
        public int Count => values.Count;

        public bool Contains(T item) => values.Contains(item);

        public IEnumerator<T> GetEnumerator() => values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public bool IsProperSubsetOf(IEnumerable<T> other) => values.IsProperSubsetOf(other);

        public bool IsProperSupersetOf(IEnumerable<T> other) => values.IsProperSupersetOf(other);

        public bool IsSubsetOf(IEnumerable<T> other) => values.IsSubsetOf(other);

        public bool IsSupersetOf(IEnumerable<T> other) => values.IsSupersetOf(other);

        public bool Overlaps(IEnumerable<T> other) => values.Overlaps(other);

        public bool SetEquals(IEnumerable<T> other) => values.SetEquals(other);
    }
}
