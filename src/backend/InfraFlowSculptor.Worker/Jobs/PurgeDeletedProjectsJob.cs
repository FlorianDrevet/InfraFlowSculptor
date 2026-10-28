using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Infrastructure.Persistence.Admin;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed partial class PurgeDeletedProjectsJob(
    ProjectPurger projectPurger,
    TimeProvider clock,
    ILogger<PurgeDeletedProjectsJob> logger) : IScheduledJob
{
    public string Name => "purge-deleted-projects";

    public TimeSpan Interval => TimeSpan.FromDays(1);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var deleted = await projectPurger.PurgeDeletedBeforeAsync(
            clock.GetUtcNow().AddDays(-30),
            cancellationToken);
        Purged(logger, deleted);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} projects past the 30-day retention period")]
    private static partial void Purged(ILogger logger, int count);
}
