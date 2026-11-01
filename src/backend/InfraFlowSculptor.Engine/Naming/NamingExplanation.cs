namespace InfraFlowSculptor.Engine.Naming;

public sealed record NamingExplanation(
    string Level,
    string Template,
    string RawName,
    string FinalName,
    IReadOnlyList<string> Transformations);
