namespace InfraFlowSculptor.Contracts.Projects;

public sealed record ProjectValidationFindingResponse(
    string Code,
    string Severity,
    string Lot,
    string SubjectType,
    Guid SubjectId,
    Guid? EnvironmentId,
    string MessageFr,
    string MessageEn,
    int? ObjectVersion,
    string? FixCode,
    bool CanApplyFix,
    IReadOnlyList<Guid> RelatedLinkIds,
    FindingAcknowledgmentResponse? Acknowledgment,
    string? DetailKey,
    string SubjectContext,
    Guid? SubjectComponentId,
    Guid? SubjectResourceId,
    string? EnvironmentCode);
