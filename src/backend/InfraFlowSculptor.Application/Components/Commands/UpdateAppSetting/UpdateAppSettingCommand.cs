using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Application.Components.Commands.UpdateAppSetting;

[ModelCommand("UpdateAppSetting")]
[ModelChange]
[RequiresPermission(Permission.ModelEdit)]
[Audited(AuditActions.AppSettingUpdated, ObjectType.AppSetting)]
public sealed record UpdateAppSettingCommand(
    Guid ProjectId,
    Guid ComponentId,
    Guid ApplicationResourceId,
    Guid AppSettingId,
    AppSettingDefinition Definition,
    int ExpectedVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
