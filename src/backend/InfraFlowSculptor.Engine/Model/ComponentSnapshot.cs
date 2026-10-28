namespace InfraFlowSculptor.Engine.Model;

public sealed record ComponentSnapshot(
    Guid Id,
    string Name,
    string Code,
    string Mode,
    IReadOnlyList<Guid> TargetEnvironmentIds,
    ComponentSingleTargetSnapshot? SingleTarget,
    IReadOnlyList<ResourceGroupSnapshot> ResourceGroups,
    IReadOnlyList<ResourceSnapshot> Resources);
