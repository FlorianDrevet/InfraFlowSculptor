namespace InfraFlowSculptor.Contracts.Organizations;

public sealed record AuditPropertyChangeResponse(
    string Name,
    string? Before,
    string? After,
    bool IsPersonalData);
