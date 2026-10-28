using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Notifications;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed class SendNotificationJobHandler(NotificationChannelRegistry channels)
    : JobHandler<SendNotificationJob>
{
    public override Task HandleAsync(
        SendNotificationJob job,
        JobExecutionContext context,
        CancellationToken cancellationToken) =>
        channels.Get(job.Channel).SendAsync(job.Email, cancellationToken);
}
