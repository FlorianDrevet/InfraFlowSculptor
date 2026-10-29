namespace InfraFlowSculptor.Contracts.Projects;

public sealed record ProjectValidationComponentResponse(
    Guid Id,
    string Code,
    string Name,
    int Position,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> UsedBy);
