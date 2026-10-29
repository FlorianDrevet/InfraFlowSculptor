using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed record WiredIdentity(
    Guid ComponentId,
    Guid ResourceId,
    LinkIdentityKind Kind,
    Guid? AttachedIdentityResourceId,
    IReadOnlyList<WiringOrigin> Origins);
