using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ResourceLinkResponse(
    Guid Id,
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
    IReadOnlyList<ResourceLinkIssueResponse> Findings,
    int? Version,
    LinkOriginSourceResponse? OriginSource);
