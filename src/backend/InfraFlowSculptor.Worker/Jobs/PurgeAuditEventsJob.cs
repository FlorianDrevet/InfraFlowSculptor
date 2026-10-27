using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Jobs;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed partial class PurgeAuditEventsJob(
    IAuditEventPurger auditEventPurger,
    TimeProvider clock,
    ILogger<PurgeAuditEventsJob> logger) : IScheduledJob
{
    public string Name => "purge-audit-events";

    public TimeSpan Interval => TimeSpan.FromDays(1);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var deleted = await auditEventPurger.PurgeAsync(clock.GetUtcNow(), cancellationToken);
        Purged(logger, deleted);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Purged {Count} audit events outside the retention period")]
    private static partial void Purged(ILogger logger, long count);
}
