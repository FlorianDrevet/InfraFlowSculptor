using InfraFlowSculptor.Domain.RevisionAggregate;

namespace InfraFlowSculptor.Application.Generation;

public sealed record RevisionView(RevisionAggregate Revision, bool IsStale, int? QueuePosition);
