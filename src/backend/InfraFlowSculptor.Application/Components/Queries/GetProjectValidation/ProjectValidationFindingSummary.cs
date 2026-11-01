namespace InfraFlowSculptor.Application.Components.Queries.GetProjectValidation;

public sealed record ProjectValidationFindingSummary(
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
    FindingAcknowledgmentSummary? Acknowledgment,
    string? DetailKey,
    string SubjectContext,
    Guid? SubjectComponentId,
    Guid? SubjectResourceId,
    string? EnvironmentCode);
