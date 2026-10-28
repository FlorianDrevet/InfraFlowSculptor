namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed class ResourcePresence
{
    public Guid TargetId { get; private set; }

    public bool IsPresent { get; private set; }

    private ResourcePresence()
    {
    }

    internal ResourcePresence(Guid environmentId, bool isPresent)
    {
        TargetId = environmentId;
        IsPresent = isPresent;
    }

    internal void Set(bool isPresent) => IsPresent = isPresent;
}
