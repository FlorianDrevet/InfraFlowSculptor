using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record LinkPreviewDataAccessResponse(
    Guid EnvironmentId,
    string EnvironmentCode,
    Guid SourceResourceId,
    string SourceResourceName,
    Guid TargetResourceId,
    string TargetResourceName,
    DataAccessLevel Level,
    IReadOnlyList<string> DatabaseRoles);
