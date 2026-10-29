namespace InfraFlowSculptor.Contracts.Resources;

public sealed record LinkPreviewResponse(
    IReadOnlyList<LinkPreviewRoleAssignmentResponse> RoleAssignments,
    IReadOnlyList<LinkPreviewIdentityResponse> Identities,
    IReadOnlyList<LinkPreviewDataAccessResponse> DataAccess,
    IReadOnlyList<LinkPreviewDependencyResponse> Dependencies,
    IReadOnlyList<LinkPreviewApplicationSettingResponse> ApplicationSettings,
    IReadOnlyList<LinkPreviewFindingResponse> Findings);
