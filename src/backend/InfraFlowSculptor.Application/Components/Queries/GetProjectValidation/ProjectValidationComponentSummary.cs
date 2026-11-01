namespace InfraFlowSculptor.Application.Components.Queries.GetProjectValidation;

public sealed record ProjectValidationComponentSummary(
    Guid Id,
    string Code,
    string Name,
    int Position,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> UsedBy);
