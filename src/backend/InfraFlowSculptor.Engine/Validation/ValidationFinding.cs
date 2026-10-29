using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Validation;

public sealed record ValidationFinding(
    string Code,
    FindingSeverity Severity,
    string Lot,
    ObjectRef Subject,
    Guid? EnvironmentId,
    string MessageFr,
    string MessageEn,
    int? ObjectVersion,
    string? FixCode,
    IReadOnlyList<Guid> RelatedLinkIds,
    string? DetailKey = null);
