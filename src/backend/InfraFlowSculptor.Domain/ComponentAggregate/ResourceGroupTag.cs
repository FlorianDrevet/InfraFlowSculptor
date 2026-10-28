namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed class ResourceGroupTag
{
    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    private ResourceGroupTag()
    {
    }

    internal ResourceGroupTag(string key, string value)
    {
        Key = key;
        Value = value;
    }
}
