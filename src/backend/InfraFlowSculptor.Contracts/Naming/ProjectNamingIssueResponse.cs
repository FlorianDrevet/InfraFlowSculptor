namespace InfraFlowSculptor.Contracts.Naming;

public sealed record ProjectNamingIssueResponse(
    string Code,
    string Message,
    string Severity,
    Guid? ComponentId,
    Guid? ResourceId,
    Guid? ResourceGroupId,
    Guid? TargetId,
    string? Value,
    int? ValueLength,
    int? Limit);
