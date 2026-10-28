using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.DeleteComponent;

[ModelCommand("DeleteComponent")]
[ModelChange]
[RequiresPermission(Permission.ComponentsManage)]
[Audited(AuditActions.ComponentDeleted, ObjectType.Component)]
public sealed record DeleteComponentCommand(Guid ProjectId, Guid ComponentId, int ExpectedVersion)
    : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
