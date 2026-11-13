namespace InfraFlowSculptor.Contracts.Revisions;

public sealed record RevisionFindingResponse(
    string Code,
    string Severity,
    string MessageFr,
    string MessageEn,
    string SubjectType,
    Guid SubjectId,
    Guid? EnvironmentId);
