namespace InfraFlowSculptor.Domain.ProjectAggregate;

public sealed class ProjectTag
{
    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    private ProjectTag()
    {
    }

    internal ProjectTag(string key, string value)
    {
        Key = key;
        Value = value;
    }

    internal void SetValue(string value) => Value = value;
}
