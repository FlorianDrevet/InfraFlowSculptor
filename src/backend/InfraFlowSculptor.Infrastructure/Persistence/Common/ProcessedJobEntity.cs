namespace InfraFlowSculptor.Infrastructure.Persistence.Common;

public sealed class ProcessedJobEntity
{
    public Guid JobId { get; set; }

    public string HandlerType { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; set; }
}
