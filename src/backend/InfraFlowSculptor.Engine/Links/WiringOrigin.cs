using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed record WiringOrigin(
    LinkOrigin LinkOrigin,
    LinkKind LinkKind,
    Guid LinkId,
    Guid SourceResourceId,
    Guid TargetResourceId);
