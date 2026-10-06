using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Infrastructure.Persistence.Admin;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed partial class PurgeIdempotencyKeysJob(
    IdempotencyKeyPurge idempotencyKeyPurge,
    TimeProvider clock,
    ILogger<PurgeIdempotencyKeysJob> logger) : IScheduledJob
{
    public string Name => "purge-idempotency-keys";

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var deleted = await idempotencyKeyPurge.DeleteExpiredAsync(
            clock.GetUtcNow().AddHours(-24),
            cancellationToken);
        Purged(logger, deleted);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} expired idempotency keys")]
    private static partial void Purged(ILogger logger, int count);
}
