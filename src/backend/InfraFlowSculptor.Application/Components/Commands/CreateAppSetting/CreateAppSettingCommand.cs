using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Application.Components.Commands.CreateAppSetting;

[ModelCommand("CreateAppSetting")]
[ModelChange]
[RequiresPermission(Permission.ModelEdit)]
[Audited(AuditActions.AppSettingCreated, ObjectType.AppSetting)]
public sealed record CreateAppSettingCommand(
    Guid ProjectId,
    Guid ComponentId,
    Guid ApplicationResourceId,
    Guid AppSettingId,
    AppSettingDefinition Definition) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
