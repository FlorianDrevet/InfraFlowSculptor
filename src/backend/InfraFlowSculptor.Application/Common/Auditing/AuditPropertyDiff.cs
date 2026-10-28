namespace InfraFlowSculptor.Application.Common.Auditing;

public sealed record AuditPropertyDiff(
    string Name,
    string? Before,
    string? After,
    bool IsPersonalData);
