namespace InfraFlowSculptor.Domain.ProjectAggregate;

public sealed class PublishPlanAssignment
{
    private PublishPlanAssignment()
    {
    }

    internal PublishPlanAssignment(Guid componentId, PublishPart part, Guid destinationId)
    {
        ComponentId = componentId;
        Part = part;
        DestinationId = destinationId;
    }

    public Guid ComponentId { get; private set; }

    public PublishPart Part { get; private set; }

    public Guid DestinationId { get; private set; }
}
