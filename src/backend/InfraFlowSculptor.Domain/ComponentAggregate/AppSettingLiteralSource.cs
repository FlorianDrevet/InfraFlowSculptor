namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed class AppSettingLiteralSource
{
    private readonly List<AppSettingLiteralOverride> overrides = [];

    public string Value { get; private set; } = string.Empty;

    public IReadOnlyCollection<AppSettingLiteralOverride> Overrides => overrides.AsReadOnly();

    private AppSettingLiteralSource()
    {
    }

    internal AppSettingLiteralSource(string value, IReadOnlyDictionary<Guid, string?>? targetOverrides)
    {
        Value = value;
        overrides.AddRange((targetOverrides ?? new Dictionary<Guid, string?>())
            .OrderBy(value => value.Key)
            .Select(value => new AppSettingLiteralOverride(value.Key, value.Value!)));
    }
}
