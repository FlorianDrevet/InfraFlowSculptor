using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Observability;

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

        if (logger.IsEnabled(LogLevel.Information))
        {
            var organizationId = IfsTelemetry.Pseudonymize(context.OrganizationId.Value);
            Processed(logger, context.JobId, organizationId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "PingJob {JobId} traité pour {OrganizationId}")]
    private static partial void Processed(ILogger logger, Guid jobId, string organizationId);
}
