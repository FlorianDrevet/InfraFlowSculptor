using InfraFlowSculptor.Domain.RevisionAggregate;

namespace InfraFlowSculptor.Application.Generation;

public sealed record RevisionGenerationResult(
    Guid RevisionId,
    int Number,
    RevisionState State,
    int? QueuePosition,
    bool IsUnchanged);
