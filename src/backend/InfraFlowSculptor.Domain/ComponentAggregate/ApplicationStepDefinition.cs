namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed class ApplicationStepDefinition
{
    private readonly List<ApplicationStepParameter> parameters = [];

    private ApplicationStepDefinition()
    {
    }

    public ApplicationStepDefinition(
        string name,
        bool enabled,
        bool blocking,
        ApplicationStepPosition position,
        IReadOnlyCollection<ApplicationStepParameter> parameters)
    {
        Name = name;
        Enabled = enabled;
        Blocking = blocking;
        Position = position;
        this.parameters.AddRange(parameters);
    }

    public string Name { get; private set; } = string.Empty;

    public bool Enabled { get; private set; }

    public bool Blocking { get; private set; }

    public ApplicationStepPosition Position { get; private set; }

    public IReadOnlyCollection<ApplicationStepParameter> Parameters => parameters.AsReadOnly();
}
