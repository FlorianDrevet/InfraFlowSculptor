using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed record WiringDependency(
    Guid SourceComponentId,
    Guid TargetComponentId,
    Guid LinkId,
    LinkKind LinkKind,
    LinkOrigin Origin);
