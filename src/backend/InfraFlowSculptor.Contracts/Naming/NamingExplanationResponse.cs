namespace InfraFlowSculptor.Contracts.Naming;

public sealed record NamingExplanationResponse(
    string Level,
    string Template,
    string RawName,
    string FinalName,
    IReadOnlyList<string> Transformations);
