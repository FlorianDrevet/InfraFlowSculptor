namespace InfraFlowSculptor.Contracts.Projects;

public sealed record EffectiveProjectPermissionsResponse(
    Guid MemberId,
    int Version,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<ProjectRoleAssignmentResponse> Assignments);
