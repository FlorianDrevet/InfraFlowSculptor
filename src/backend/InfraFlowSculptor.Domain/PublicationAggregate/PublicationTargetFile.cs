namespace InfraFlowSculptor.Domain.PublicationAggregate;

public sealed class PublicationTargetFile
{
    private PublicationTargetFile()
    {
    }

    internal PublicationTargetFile(
        Guid destinationId,
        string repositoryPath,
        string? sha256,
        PublicationFileAction action,
        string? existingSha256)
    {
        Id = Guid.CreateVersion7();
        DestinationId = destinationId;
        RepositoryPath = repositoryPath;
        Sha256 = sha256;
        Action = action;
        ExistingSha256 = existingSha256;
    }

    public Guid Id { get; private set; }

    public Guid DestinationId { get; private set; }

    public string RepositoryPath { get; private set; } = string.Empty;

    public string? Sha256 { get; private set; }

    public PublicationFileAction Action { get; private set; }

    public string? ExistingSha256 { get; private set; }

    internal void ReplaceContentHash(string sha256) => Sha256 = sha256;
}
