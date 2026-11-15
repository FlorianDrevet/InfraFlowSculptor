namespace InfraFlowSculptor.Engine.Plan;

public sealed record InstallationChecklistSection(
    string Code,
    string Title,
    string Subtitle,
    IReadOnlyList<InstallationChecklistItem> Items);
