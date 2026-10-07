using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Events;

public sealed record DomainEventExecutionContext(
    Guid EventId,
    OrganizationId OrganizationId,
    DateTimeOffset StartedAt);
