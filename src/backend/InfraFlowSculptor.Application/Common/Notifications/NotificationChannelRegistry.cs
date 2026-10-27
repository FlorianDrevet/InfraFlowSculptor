using InfraFlowSculptor.Application.Common.Extensibility;

namespace InfraFlowSculptor.Application.Common.Notifications;

public sealed class NotificationChannelRegistry
{
    private readonly Registry<NotificationChannel, INotificationChannel> registry;

    public NotificationChannelRegistry(IEnumerable<INotificationChannel> channels)
    {
        registry = new Registry<NotificationChannel, INotificationChannel>(channels);
    }

    public INotificationChannel Get(NotificationChannel channel) => registry.Get(channel);
}
