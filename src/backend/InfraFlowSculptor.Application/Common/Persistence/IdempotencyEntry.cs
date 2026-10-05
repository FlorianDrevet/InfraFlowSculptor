using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Persistence;

public sealed record IdempotencyEntry(
    OrganizationId OrganizationId,
    Guid Key,
    string RequestHash,
    IdempotencyResponse? Response);
