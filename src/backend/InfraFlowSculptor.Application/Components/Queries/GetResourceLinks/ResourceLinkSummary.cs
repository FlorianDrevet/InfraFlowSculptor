using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceLinks;

public sealed record ResourceLinkSummary(
    Guid Id,
    int? Version,
    LinkKind Kind,
    string KindLabelFr,
    string KindLabelEn,
    LinkOrigin Origin,
    Guid SourceComponentId,
    string SourceComponentCode,
    Guid SourceResourceId,
    string SourceResourceName,
    Guid TargetComponentId,
    string TargetComponentCode,
    Guid TargetResourceId,
    string TargetResourceName,
    LinkIdentityKind IdentityKind,
    Guid? IdentityResourceId,
    LinkAccessScope AccessScope,
    DataAccessLevel? DataAccessLevel,
    IReadOnlyList<string> Roles,
    bool IsExternal,
    bool CreatesDeploymentDependency,
    IReadOnlyList<ResourceLinkIssueSummary> Findings,
    LinkOriginSource? OriginSource);
