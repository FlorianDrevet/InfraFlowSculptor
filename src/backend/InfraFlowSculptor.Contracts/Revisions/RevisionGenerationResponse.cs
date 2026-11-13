namespace InfraFlowSculptor.Contracts.Revisions;

public sealed record RevisionGenerationResponse(
    Guid RevisionId,
    int Number,
    string State,
    int? QueuePosition,
    bool IsUnchanged);
