using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Components.Commands.RemoveLink;

[ModelCommand("RemoveLink")]
[ModelChange]
[RequiresPermission(Permission.ModelEdit)]
[Audited(AuditActions.LinkDeleted, ObjectType.Link)]
public sealed record RemoveLinkCommand(
    Guid ProjectId,
    Guid ComponentId,
    Guid ResourceId,
    Guid LinkId,
    int ExpectedLinkVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
