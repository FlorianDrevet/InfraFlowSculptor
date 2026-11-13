namespace InfraFlowSculptor.Contracts.Revisions;

public sealed record RevisionDetailResponse(
    RevisionSummaryResponse Revision,
    IReadOnlyList<RevisionFindingResponse> Findings,
    IReadOnlyList<RevisionFileResponse> Files,
    IReadOnlyList<RevisionChangeResponse> Changes);
