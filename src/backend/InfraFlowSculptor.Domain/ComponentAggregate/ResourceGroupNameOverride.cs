namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed class ResourceGroupNameOverride
{
    public Guid EnvironmentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    private ResourceGroupNameOverride()
    {
    }

    internal ResourceGroupNameOverride(Guid environmentId, string name)
    {
        EnvironmentId = environmentId;
        Name = name;
    }
}
