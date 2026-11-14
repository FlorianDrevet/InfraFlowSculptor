namespace InfraFlowSculptor.Application.Publications;

public sealed record PublicationTargetFileView(
    Guid DestinationId,
    string Path,
    string Action,
    string? Sha256,
    string? ExistingSha256);
