namespace InfraFlowSculptor.Application.Common.Auditing;

public sealed record AuditEventPage(
    IReadOnlyList<AuditEventItem> Items,
    int Page,
    int PageSize,
    long TotalCount);
