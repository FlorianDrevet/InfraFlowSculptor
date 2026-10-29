using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Application.Components.Commands.AddLink;

[ModelCommand("AddLink")]
[ModelChange]
[RequiresPermission(Permission.ModelEdit)]
[Audited(AuditActions.LinkCreated, ObjectType.Link)]
public sealed record AddLinkCommand(
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
    int ExpectedComponentVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
