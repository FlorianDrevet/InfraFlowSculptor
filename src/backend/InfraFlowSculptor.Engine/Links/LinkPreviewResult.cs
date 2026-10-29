namespace InfraFlowSculptor.Engine.Links;

public sealed record LinkPreviewResult(
    IReadOnlyList<LinkPreviewRoleAssignment> RoleAssignments,
    IReadOnlyList<LinkPreviewIdentity> Identities,
    IReadOnlyList<LinkPreviewDataAccess> DataAccess,
    IReadOnlyList<LinkPreviewDependency> Dependencies,
    IReadOnlyList<LinkPreviewApplicationSetting> ApplicationSettings,
    IReadOnlyList<LinkPreviewFinding> Findings);
