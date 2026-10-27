using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Auditing;

public interface IAuditEventReader
{
    Task<AuditEventPage> ListAsync(
        OrganizationId organizationId,
        DateTimeOffset? from,
        DateTimeOffset? through,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
