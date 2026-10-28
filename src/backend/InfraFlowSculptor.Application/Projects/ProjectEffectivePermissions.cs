namespace InfraFlowSculptor.Application.Projects;

public sealed record ProjectEffectivePermissions(
    Guid MemberId,
    int Version,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<ProjectRoleAssignmentSummary> Assignments);
