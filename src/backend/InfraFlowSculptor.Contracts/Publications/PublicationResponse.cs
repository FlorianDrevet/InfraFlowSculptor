namespace InfraFlowSculptor.Contracts.Publications;

public sealed record PublicationResponse(
    Guid Id,
    Guid ProjectId,
    Guid RevisionId,
    int RevisionNumber,
    string Message,
    string State,
    DateTimeOffset RequestedAt,
    DateTimeOffset? PreparedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyCollection<PublicationTargetResponse> Targets);
