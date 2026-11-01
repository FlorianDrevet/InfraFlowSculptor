using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record LinkPreviewDependencyResponse(
    Guid EnvironmentId,
    string EnvironmentCode,
    Guid SourceComponentId,
    string SourceComponentCode,
    Guid TargetComponentId,
    string TargetComponentCode,
    LinkKind Kind);
