namespace InfraFlowSculptor.Application.Projects.Installation;

public sealed record InstallationChecklistSectionView(
    string Code,
    string Title,
    string Subtitle,
    IReadOnlyList<InstallationChecklistItemView> Items);
