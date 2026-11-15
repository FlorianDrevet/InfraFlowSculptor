namespace InfraFlowSculptor.Contracts.Projects;

public sealed record InstallationChecklistSectionResponse(
    string Code,
    string Title,
    string Subtitle,
    IReadOnlyList<InstallationChecklistItemResponse> Items);
