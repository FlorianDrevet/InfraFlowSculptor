using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Components.Commands.SetComponentDefaultLogAnalytics;

[ModelCommand("SetComponentDefaultLogAnalytics")]
[ModelChange]
[RequiresPermission(Permission.ComponentsManage)]
[Audited(AuditActions.ComponentUpdated, ObjectType.Component)]
public sealed record SetComponentDefaultLogAnalyticsCommand(
    Guid ProjectId,
    Guid ComponentId,
    Guid? WorkspaceResourceId,
    int ExpectedVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
