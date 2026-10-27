namespace InfraFlowSculptor.Contracts.Naming;

public sealed record NamingPreviewResponse(
    IReadOnlyList<NamingPreviewItemResponse> Items,
    IReadOnlyList<NamingPreviewIssueResponse> Issues);
