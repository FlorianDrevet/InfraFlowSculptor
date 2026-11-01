using InfraFlowSculptor.Domain.Common.Extensibility;

namespace InfraFlowSculptor.Application.Common.Notifications;

public interface INotificationChannel : IKeyed<NotificationChannel>
{
    Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken);
}
