namespace InfraFlowSculptor.Engine.Model;

public sealed record ModelSnapshot(
    ProjectSnapshot Project,
    IReadOnlyList<EnvironmentSnapshot> Environments,
    NamingConventionSnapshot Naming,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<ComponentSnapshot> Components);
