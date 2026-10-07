using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Jobs;

public sealed record JobExecutionContext(
    Guid JobId,
    OrganizationId OrganizationId,
    DateTimeOffset StartedAt);
