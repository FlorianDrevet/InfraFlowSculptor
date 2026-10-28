namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed class ResourceTag
{
    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    private ResourceTag()
    {
    }

    internal ResourceTag(string key, string value)
    {
        Key = key;
        Value = value;
    }
}
