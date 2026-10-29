using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.PreviewLink;

[RequiresPermission(Permission.ProjectRead)]
public sealed record PreviewLinkQuery(
    Guid ProjectId,
    Guid ComponentId,
    Guid SourceResourceId,
    Guid TargetComponentId,
    Guid TargetResourceId,
    LinkKind Kind,
    LinkIdentityKind IdentityKind,
    Guid? IdentityResourceId,
    IReadOnlyList<string> Roles,
    LinkAccessScope AccessScope,
    DataAccessLevel? DataAccessLevel)
    : IQuery<ErrorOr<LinkPreviewResult>>, IProjectScopedRequest;
