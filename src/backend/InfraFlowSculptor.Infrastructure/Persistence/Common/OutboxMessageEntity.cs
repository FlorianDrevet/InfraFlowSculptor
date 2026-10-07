using System.Text.Json;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Infrastructure.Persistence.Common;

public sealed class OutboxMessageEntity : IOrganizationOwned
{
    public const int MaximumAttempts = 10;

    private OutboxMessageEntity()
    {
        Payload = default;
    }

    public OutboxMessageEntity(OutboxMessageData message)
    {
        Id = message.Id;
        OrganizationId = message.OrganizationId;
        Queue = message.Queue;
        SessionId = message.SessionId;
        Type = message.Type;
        Payload = message.Payload;
        CreatedAt = message.CreatedAt;
        NextAttemptAt = message.CreatedAt;
    }

    public Guid Id { get; private set; }

    public OrganizationId OrganizationId { get; private set; }

    public string Queue { get; private set; } = string.Empty;

    public string SessionId { get; private set; } = string.Empty;

    public string Type { get; private set; } = string.Empty;

    public JsonElement Payload { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? FailedAt { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public void MarkSent(DateTimeOffset sentAt) => SentAt = sentAt;

    public bool RecordFailure(DateTimeOffset now)
    {
        Attempts++;
        if (Attempts >= MaximumAttempts)
        {
            FailedAt = now;
            NextAttemptAt = null;
            return true;
        }

        var retrySeconds = Math.Min(Math.Pow(2, Math.Min(Attempts, 8)), 300);
        NextAttemptAt = now.AddSeconds(retrySeconds);
        return false;
    }
}
