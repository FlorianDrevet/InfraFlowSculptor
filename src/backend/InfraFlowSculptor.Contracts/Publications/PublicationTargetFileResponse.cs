namespace InfraFlowSculptor.Contracts.Publications;

public sealed record PublicationTargetFileResponse(
    Guid DestinationId,
    string Path,
    string Action,
    string? Sha256,
    string? ExistingSha256);
