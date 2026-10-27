namespace InfraFlowSculptor.Application.Common.Auditing;

public sealed record AuditEventDiff(IReadOnlyList<AuditPropertyDiff> Changes);
