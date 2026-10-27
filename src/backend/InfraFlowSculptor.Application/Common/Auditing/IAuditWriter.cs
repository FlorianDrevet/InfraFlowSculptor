namespace InfraFlowSculptor.Application.Common.Auditing;

public interface IAuditWriter
{
    Task WriteAsync(AuditedAttribute audit, CancellationToken cancellationToken);
}
