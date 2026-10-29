namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed class AppSettingLiteralOverride
{
    public Guid TargetId { get; private set; }

    public string Value { get; private set; } = string.Empty;

    private AppSettingLiteralOverride()
    {
    }

    internal AppSettingLiteralOverride(Guid targetId, string value)
    {
        TargetId = targetId;
        Value = value;
    }
}
