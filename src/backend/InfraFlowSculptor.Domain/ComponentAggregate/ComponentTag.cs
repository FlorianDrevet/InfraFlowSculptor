namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed class ComponentTag
{
    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    private ComponentTag()
    {
    }

    internal ComponentTag(string key, string value)
    {
        Key = key;
        Value = value;
    }
}
