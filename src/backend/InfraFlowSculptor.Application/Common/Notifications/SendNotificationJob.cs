using InfraFlowSculptor.Application.Common.Jobs;

namespace InfraFlowSculptor.Application.Common.Notifications;

public sealed record SendNotificationJob(NotificationChannel Channel, OutgoingEmail Email) : IJob;
