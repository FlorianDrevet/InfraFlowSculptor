using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Naming;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Components.Commands.SetComponentNaming;

[ModelCommand("SetComponentNaming")]
[ModelChange]
[RequiresPermission(Permission.ComponentsManage)]
[Audited(AuditActions.ComponentUpdated, ObjectType.Component)]
public sealed record SetComponentNamingCommand(
    Guid ProjectId,
    Guid ComponentId,
    string? DefaultTemplate,
    IReadOnlyList<NamingOverrideValue> TypeOverrides,
    int ExpectedVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
