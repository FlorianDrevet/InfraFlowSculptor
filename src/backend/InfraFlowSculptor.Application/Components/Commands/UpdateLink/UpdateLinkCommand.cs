using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Application.Components.Commands.UpdateLink;

[ModelCommand("UpdateLink")]
[ModelChange]
[RequiresPermission(Permission.ModelEdit)]
[Audited(AuditActions.LinkUpdated, ObjectType.Link)]
public sealed record UpdateLinkCommand(
    Guid ProjectId,
    Guid ComponentId,
    Guid LinkId,
    Guid SourceResourceId,
    Guid TargetComponentId,
    Guid TargetResourceId,
    LinkKind Kind,
    LinkIdentityKind IdentityKind,
    Guid? IdentityResourceId,
    IReadOnlyList<string> Roles,
    LinkAccessScope AccessScope,
    DataAccessLevel? DataAccessLevel,
    int ExpectedLinkVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
