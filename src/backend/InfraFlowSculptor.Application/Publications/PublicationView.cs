namespace InfraFlowSculptor.Application.Publications;

public sealed record PublicationView(
    Guid Id,
    Guid ProjectId,
    Guid RevisionId,
    int RevisionNumber,
    string Message,
    string State,
    DateTimeOffset RequestedAt,
    DateTimeOffset? PreparedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyCollection<PublicationTargetView> Targets);
