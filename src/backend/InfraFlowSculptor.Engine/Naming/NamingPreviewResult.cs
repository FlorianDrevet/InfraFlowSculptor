namespace InfraFlowSculptor.Engine.Naming;

public sealed record NamingPreviewResult(
    IReadOnlyList<NamingPreviewItem> Items,
    IReadOnlyList<NamingIssue> Issues);
