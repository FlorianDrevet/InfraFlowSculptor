using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Components.Commands.DeleteAppSetting;

[ModelCommand("DeleteAppSetting")]
[ModelChange]
[RequiresPermission(Permission.ModelEdit)]
[Audited(AuditActions.AppSettingDeleted, ObjectType.AppSetting)]
public sealed record DeleteAppSettingCommand(
    Guid ProjectId,
    Guid ComponentId,
    Guid ApplicationResourceId,
    Guid AppSettingId,
    int ExpectedVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
