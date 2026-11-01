namespace InfraFlowSculptor.Engine.Plan;

public sealed record ExposurePlan
{
    public ExposurePlan(string mode, IReadOnlyList<string> allowedIpRanges)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(allowedIpRanges);
        Mode = mode;
        AllowedIpRanges = Array.AsReadOnly(allowedIpRanges
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());
    }

    public string Mode { get; init; }

    public IReadOnlyList<string> AllowedIpRanges { get; init; }
}
