using System.Text.Json;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Infrastructure.Persistence.Common;

public sealed class IdempotencyKeyEntity : IOrganizationOwned
{
    public IdempotencyKeyEntity()
    {
    }

    public IdempotencyKeyEntity(
        OrganizationId organizationId,
        Guid key,
        string requestHash,
        DateTimeOffset createdAt)
    {
        OrganizationId = organizationId;
        Key = key;
        RequestHash = requestHash;
        CreatedAt = createdAt;
    }

    public OrganizationId OrganizationId { get; set; }

    public Guid Key { get; set; }

    public string RequestHash { get; set; } = string.Empty;

    public int? StatusCode { get; set; }

    public JsonElement? Response { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
