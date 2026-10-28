using System.Collections.ObjectModel;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Domain.OrganizationAggregate;

public static class ProjectRoles
{
    private static readonly ReadOnlyDictionary<ProjectRole, IReadOnlySet<string>> PermissionsByRole =
        new ReadOnlyDictionary<ProjectRole, IReadOnlySet<string>>(
            new Dictionary<ProjectRole, IReadOnlySet<string>>
            {
                [ProjectRole.Reader] = Set(Permission.ProjectRead),
                [ProjectRole.Auditor] = Set(Permission.ProjectRead, Permission.AuditRead),
                [ProjectRole.Developer] = Set(
                    Permission.ProjectRead,
                    Permission.ModelEdit,
                    Permission.ApplicationsManage,
                    Permission.Generate),
                [ProjectRole.Contributor] = Set(
                    Permission.ProjectRead,
                    Permission.ModelEdit,
                    Permission.ApplicationsManage,
                    Permission.ComponentsManage,
                    Permission.Generate,
                    Permission.Publish,
                    Permission.ApplyProposals),
                [ProjectRole.DeploymentManager] = Set(
                    Permission.ProjectRead,
                    Permission.Generate,
                    Permission.Publish,
                    Permission.InstallationManage,
                    Permission.EnvironmentsManage),
                [ProjectRole.PlatformArchitect] = Set(
                    Permission.ProjectRead,
                    Permission.ConventionsManage,
                    Permission.ComponentsManage,
                    Permission.ModelEdit,
                    Permission.ApplicationsManage,
                    Permission.EnvironmentsManage,
                    Permission.ProtectedEnvironmentsManage,
                    Permission.PublicationManage,
                    Permission.Generate,
                    Permission.Publish,
                    Permission.InstallationManage,
                    Permission.ApplyProposals),
                [ProjectRole.ProjectAdministrator] = Set(
                    Permission.All.Where(permission => permission != Permission.ProjectAdminister).ToArray()),
                [ProjectRole.Owner] = Set(Permission.All.ToArray())
            });

    public static IReadOnlySet<string> PermissionsFor(ProjectRole role) =>
        PermissionsByRole.TryGetValue(role, out var permissions)
            ? permissions
            : throw new ArgumentOutOfRangeException(nameof(role), role, "The project role is not supported.");

    public static bool CanAssign(
        IReadOnlySet<string> actorPermissions,
        bool actorIsOwner,
        ProjectRole role) => MissingPermissionToAssign(actorPermissions, actorIsOwner, role) is null;

    public static string? MissingPermissionToAssign(
        IReadOnlySet<string> actorPermissions,
        bool actorIsOwner,
        ProjectRole role)
    {
        if (role == ProjectRole.Owner && !actorIsOwner)
        {
            return Permission.ProjectAdminister;
        }

        return PermissionsFor(role).FirstOrDefault(permission => !actorPermissions.Contains(permission));
    }

    private static ReadOnlySet<string> Set(params string[] permissions) =>
        new ReadOnlySet<string>(permissions.ToHashSet(StringComparer.Ordinal));

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
