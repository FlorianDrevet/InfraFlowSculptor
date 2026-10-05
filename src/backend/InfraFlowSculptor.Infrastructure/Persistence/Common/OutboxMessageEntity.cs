using System.Text.Json;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Infrastructure.Persistence.Common;

public sealed class OutboxMessageEntity : IOrganizationOwned
{
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
    }

    public Guid Id { get; private set; }

    public OrganizationId OrganizationId { get; private set; }

    public string Queue { get; private set; } = string.Empty;

    public string SessionId { get; private set; } = string.Empty;

    public string Type { get; private set; } = string.Empty;

    public JsonElement Payload { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public int Attempts { get; private set; }
}
