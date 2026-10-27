namespace InfraFlowSculptor.Contracts.Organizations;

public sealed record AuditEventDiffResponse(IReadOnlyList<AuditPropertyChangeResponse> Changes);
