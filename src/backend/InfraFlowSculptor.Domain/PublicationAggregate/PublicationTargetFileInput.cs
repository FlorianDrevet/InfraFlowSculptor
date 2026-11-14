namespace InfraFlowSculptor.Domain.PublicationAggregate;

public sealed record PublicationTargetFileInput(
    Guid DestinationId,
    string RepositoryPath,
    string? Sha256,
    PublicationFileAction Action,
    string? ExistingSha256);
