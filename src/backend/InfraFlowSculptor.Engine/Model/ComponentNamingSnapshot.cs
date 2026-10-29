namespace InfraFlowSculptor.Engine.Model;

public sealed record ComponentNamingSnapshot(
    string? DefaultTemplate,
    IReadOnlyDictionary<string, string> TypeTemplates,
    IReadOnlyDictionary<string, string> TypeAbbreviations);
