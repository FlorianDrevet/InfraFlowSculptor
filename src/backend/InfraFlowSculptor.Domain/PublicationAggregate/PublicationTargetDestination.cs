namespace InfraFlowSculptor.Domain.PublicationAggregate;

public sealed class PublicationTargetDestination
{
    private PublicationTargetDestination()
    {
    }

    internal PublicationTargetDestination(Guid destinationId, string basePath)
    {
        DestinationId = destinationId;
        BasePath = basePath;
    }

    public Guid DestinationId { get; private set; }

    public string BasePath { get; private set; } = string.Empty;
}
