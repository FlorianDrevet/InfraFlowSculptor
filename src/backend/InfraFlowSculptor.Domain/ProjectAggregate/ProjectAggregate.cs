using ErrorOr;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Domain.ProjectAggregate;

[AuditedObject(ObjectType.Project, nameof(ProjectAggregate.Id), nameof(ProjectAggregate.OrganizationId))]
public sealed class ProjectAggregate : AggregateRoot<ProjectId>, IOrganizationOwned, IHasVersion
{
    private readonly List<ProjectRoleAssignment> roleAssignments = [];

    public OrganizationId OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public int Version { get; private set; } = 1;

    public DateTimeOffset? DeletedAt { get; private set; }

    public IReadOnlyCollection<ProjectRoleAssignment> RoleAssignments => roleAssignments.AsReadOnly();

    private ProjectAggregate()
    {
    }

    private ProjectAggregate(
        ProjectId id,
        OrganizationId organizationId,
        string name,
        string code) : base(id)
    {
        OrganizationId = organizationId;
        Name = name;
        Code = code;
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
            || creatorProfileId.Value == Guid.Empty
            || normalizedName.Length == 0
            || normalizedCode.Length == 0)
        {
            return Error.Validation("PROJECT_IDENTITY_REQUIRED", "A project, organization, name, code, and creator are required.");
        }

        var project = new ProjectAggregate(id, organizationId, normalizedName, normalizedCode);
        project.roleAssignments.Add(new ProjectRoleAssignment(
            organizationId,
            creatorProfileId,
            ProjectRole.Owner,
            ProjectRoleScope.Project));
        return project;
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
