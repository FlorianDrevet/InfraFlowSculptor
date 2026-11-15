namespace InfraFlowSculptor.Contracts.Publications;

public sealed record StartRevisionPublicationRequest(
    string? Message,
    bool AcceptStaleRevision,
    IReadOnlyCollection<Guid>? DestinationIds = null);
