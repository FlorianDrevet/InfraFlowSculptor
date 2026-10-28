namespace InfraFlowSculptor.Engine.Naming;

public sealed record NamingPreviewItem(string EnvironmentCode, string ResourceName, string ResourceGroupName)
{
    public Guid ComponentId { get; init; }

    public string ComponentCode { get; init; } = string.Empty;

    public Guid ResourceId { get; init; }

    public Guid ResourceGroupId { get; init; }

    public string ResourceType { get; init; } = string.Empty;

    public string LogicalName { get; init; } = string.Empty;

    public Guid TargetId { get; init; }

    public NamingExplanation? Explanation { get; init; }

    public NamingExplanation? ResourceGroupExplanation { get; init; }

    public bool IsExisting { get; init; }

    public int NameLength { get; init; }

    public int? MaximumLength { get; init; }
}
