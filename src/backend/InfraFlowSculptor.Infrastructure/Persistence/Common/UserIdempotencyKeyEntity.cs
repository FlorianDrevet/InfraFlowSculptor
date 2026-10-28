using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Infrastructure.Persistence.Common;

public sealed class UserIdempotencyKeyEntity
{
    public UserIdempotencyKeyEntity()
    {
    }

    public UserIdempotencyKeyEntity(
        UserKey userKey,
        Guid key,
        string requestHash,
        DateTimeOffset createdAt)
    {
        TenantId = userKey.TenantId;
        UserObjectId = userKey.ObjectId;
        Key = key;
        RequestHash = requestHash;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; set; }

    public Guid UserObjectId { get; set; }

    public Guid Key { get; set; }

    public string RequestHash { get; set; } = string.Empty;

    public int? StatusCode { get; set; }

    public System.Text.Json.JsonElement? Response { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
