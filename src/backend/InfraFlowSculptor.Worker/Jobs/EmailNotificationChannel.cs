using InfraFlowSculptor.Application.Common.Notifications;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed class EmailNotificationChannel(IEmailSender emailSender) : INotificationChannel
{
    public NotificationChannel Key => NotificationChannel.Email;

    public Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken) =>
        emailSender.SendAsync(email, cancellationToken);
}
