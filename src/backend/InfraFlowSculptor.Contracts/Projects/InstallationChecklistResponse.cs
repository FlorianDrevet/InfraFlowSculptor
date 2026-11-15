namespace InfraFlowSculptor.Contracts.Projects;

public sealed record InstallationChecklistResponse(
    int? RevisionNumber,
    IReadOnlyList<InstallationChecklistSectionResponse> Sections);
