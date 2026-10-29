namespace InfraFlowSculptor.Engine.Model;

public sealed record ProjectSnapshot(string Name, string Code, string Description)
{
    public Guid Id { get; init; }

    public int Version { get; init; } = 1;

    public int ModelVersion { get; init; }

    public string CatalogVersion { get; init; } = string.Empty;

    public bool SystemTagsEnabled { get; init; } = true;

    public string? PublishingPreset { get; init; }
}
