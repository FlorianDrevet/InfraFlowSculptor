namespace InfraFlowSculptor.Contracts.Revisions;

public sealed record RevisionListResponse(IReadOnlyList<RevisionSummaryResponse> Items, int NextNumber);
