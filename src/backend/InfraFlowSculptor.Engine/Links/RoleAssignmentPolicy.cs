namespace InfraFlowSculptor.Engine.Links;

public static class RoleAssignmentPolicy
{
    private static readonly HashSet<string> ForbiddenRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Owner",
        "User Access Administrator",
        "Role Based Access Control Administrator"
    };

    public static bool IsForbiddenRole(string role) => ForbiddenRoles.Contains(role.Trim());
}
