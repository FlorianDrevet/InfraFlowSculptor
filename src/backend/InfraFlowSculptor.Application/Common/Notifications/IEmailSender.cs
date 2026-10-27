namespace InfraFlowSculptor.Application.Common.Notifications;

public interface IEmailSender
{
    Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken);
}
