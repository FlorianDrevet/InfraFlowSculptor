namespace InfraFlowSculptor.Contracts.Naming;

public sealed record ProjectNamingPreviewResponse(
    IReadOnlyList<ProjectNamingPreviewItemResponse> Items,
    IReadOnlyList<ProjectNamingIssueResponse> Issues);
