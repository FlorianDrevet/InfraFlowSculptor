using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Ordering;

public sealed record ComponentOrderingResult(
    IReadOnlyList<ComponentSnapshot> OrderedComponents,
    IReadOnlyList<ComponentDependencyCycle> Cycles,
    WiringResult Wiring);
