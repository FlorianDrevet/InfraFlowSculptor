using System.Text.Json;
using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Persistence;

public sealed record OutboxMessageData(
    Guid Id,
    OrganizationId OrganizationId,
    string Queue,
    string SessionId,
    string Type,
    JsonElement Payload,
    DateTimeOffset CreatedAt);
