namespace InfraFlowSculptor.Application.Projects.Installation;

public sealed record InstallationChecklistView(
    int? RevisionNumber,
    IReadOnlyList<InstallationChecklistSectionView> Sections);
