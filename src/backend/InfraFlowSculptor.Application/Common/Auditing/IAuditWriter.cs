using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Auditing;

public interface IAuditWriter
{
    Task WriteAsync(
        AuditedAttribute audit,
        Guid? projectId,
        OrganizationId? organizationId,
        CancellationToken cancellationToken);
}
