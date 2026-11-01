using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed record LinkPreviewDataAccess(
    Guid EnvironmentId,
    string EnvironmentCode,
    Guid SourceResourceId,
    string SourceResourceName,
    Guid TargetResourceId,
    string TargetResourceName,
    DataAccessLevel Level,
    IReadOnlyList<string> DatabaseRoles);
