namespace InfraFlowSculptor.Contracts.Organizations;

public sealed record AuditEventPageResponse(
    IReadOnlyList<AuditEventResponse> Items,
    int Page,
    int PageSize,
    long TotalCount);
