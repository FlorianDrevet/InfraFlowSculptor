using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed record DataAccessRequest(
    Guid OwnerComponentId,
    Guid SourceResourceId,
    Guid TargetComponentId,
    Guid TargetResourceId,
    DataEngine Engine,
    DataAccessLevel Level,
    LinkIdentityKind IdentityKind,
    Guid? IdentityResourceId,
    IReadOnlyList<string> DatabaseRoles,
    string ApplyScript,
    string RemoveScript,
    WiringOrigin Origin);
