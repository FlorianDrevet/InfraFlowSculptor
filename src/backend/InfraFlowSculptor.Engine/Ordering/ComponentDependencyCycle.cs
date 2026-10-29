using InfraFlowSculptor.Engine.Links;

namespace InfraFlowSculptor.Engine.Ordering;

public sealed record ComponentDependencyCycle(
    IReadOnlyList<Guid> ComponentIds,
    IReadOnlyList<WiringDependency> Dependencies);
