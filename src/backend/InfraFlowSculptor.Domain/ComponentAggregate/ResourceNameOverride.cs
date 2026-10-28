namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed class ResourceNameOverride
{
    public Guid TargetId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    private ResourceNameOverride()
    {
    }

    internal ResourceNameOverride(Guid environmentId, string name)
    {
        TargetId = environmentId;
        Name = name;
    }

    internal void Set(string name) => Name = name;
}
