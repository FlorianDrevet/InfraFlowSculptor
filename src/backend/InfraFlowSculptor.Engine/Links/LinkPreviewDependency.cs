using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed record LinkPreviewDependency(
    Guid EnvironmentId,
    string EnvironmentCode,
    Guid SourceComponentId,
    string SourceComponentCode,
    Guid TargetComponentId,
    string TargetComponentCode,
    LinkKind Kind);
