namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed class ApplicationStepParameter
{
    private ApplicationStepParameter()
    {
    }

    public ApplicationStepParameter(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;
}
