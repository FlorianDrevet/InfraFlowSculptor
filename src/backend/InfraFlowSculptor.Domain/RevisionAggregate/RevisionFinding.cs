namespace InfraFlowSculptor.Domain.RevisionAggregate;

public sealed record RevisionFinding(
    string Code,
    RevisionFindingSeverity Severity,
    string MessageFr,
    string MessageEn,
    string SubjectType,
    Guid SubjectId,
    Guid? EnvironmentId);
