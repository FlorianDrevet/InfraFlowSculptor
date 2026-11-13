using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Infrastructure.Persistence.Revisions;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed partial class PurgeRevisionFilesJob(
    RevisionFilePurger revisionFilePurger,
    TimeProvider clock,
    ILogger<PurgeRevisionFilesJob> logger) : IScheduledJob
{
    public string Name => "purge-revision-files";

    public TimeSpan Interval => TimeSpan.FromDays(1);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddDays(-90);
        var purged = await revisionFilePurger.PurgeAsync(cutoff, cancellationToken);
        Purged(logger, purged);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged revision files for {Count} expired revisions")]
    private static partial void Purged(ILogger logger, int count);
}
