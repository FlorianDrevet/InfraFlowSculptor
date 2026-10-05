namespace InfraFlowSculptor.Infrastructure.Persistence.Common;

public sealed class ScheduledJobLeaseEntity
{
    public string JobName { get; set; } = string.Empty;

    public string Holder { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
}
