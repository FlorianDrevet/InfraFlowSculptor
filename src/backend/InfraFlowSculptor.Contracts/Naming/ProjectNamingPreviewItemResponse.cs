namespace InfraFlowSculptor.Contracts.Naming;

public sealed record ProjectNamingPreviewItemResponse(
    Guid ComponentId,
    string ComponentCode,
    Guid ResourceId,
    string ResourceType,
    string LogicalName,
    Guid TargetId,
    string TargetCode,
    string ResourceName,
    string ResourceGroupName,
    int NameLength,
    int? MaximumLength,
    bool IsExisting,
    NamingExplanationResponse? Explanation,
    NamingExplanationResponse? ResourceGroupExplanation);
