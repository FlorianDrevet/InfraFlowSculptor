namespace InfraFlowSculptor.Application.Common.Auditing;

public interface IAuditEventPurger
{
    Task<long> PurgeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}
