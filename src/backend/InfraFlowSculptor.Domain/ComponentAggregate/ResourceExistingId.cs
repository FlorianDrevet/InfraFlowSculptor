namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed class ResourceExistingId
{
    public Guid TargetId { get; private set; }

    public string Value { get; private set; } = string.Empty;

    private ResourceExistingId()
    {
    }

    internal ResourceExistingId(Guid environmentId, string value)
    {
        TargetId = environmentId;
        Value = value;
    }

    internal void Set(string value) => Value = value;
}
