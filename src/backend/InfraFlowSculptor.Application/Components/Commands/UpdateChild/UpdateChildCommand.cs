using System.Text.Json;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Components.Commands.UpdateChild;

[ModelCommand("UpdateChild")]
[ModelChange]
[RequiresPermission(Permission.ModelEdit)]
[Audited(AuditActions.ResourceUpdated, ObjectType.Resource)]
public sealed record UpdateChildCommand(
    Guid ProjectId,
    Guid ComponentId,
    Guid ResourceId,
    Guid ChildId,
    string Name,
    JsonElement Properties,
    int ExpectedVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
