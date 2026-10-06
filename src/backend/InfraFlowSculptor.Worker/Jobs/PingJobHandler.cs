using InfraFlowSculptor.Application.Common.Jobs;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed partial class PingJobHandler(ILogger<PingJobHandler> logger) : JobHandler<PingJob>
{
    public override async Task HandleAsync(
        PingJob job,
        JobExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (job.DelayMilliseconds > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(job.DelayMilliseconds), cancellationToken);
        }

        Processed(logger, context.JobId, context.OrganizationId.Value);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "PingJob {JobId} traité pour {OrganizationId}")]
    private static partial void Processed(ILogger logger, Guid jobId, Guid organizationId);
}
