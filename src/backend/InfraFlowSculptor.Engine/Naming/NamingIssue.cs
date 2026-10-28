namespace InfraFlowSculptor.Engine.Naming;

public sealed record NamingIssue(string Code, string Message)
{
    public string Severity { get; init; } = NamingIssueSeverity.Error;

    public Guid? ComponentId { get; init; }

    public Guid? ResourceId { get; init; }

    public Guid? ResourceGroupId { get; init; }

    public Guid? TargetId { get; init; }

    public string? Value { get; init; }

    public int? ValueLength { get; init; }

    public int? Limit { get; init; }
}
