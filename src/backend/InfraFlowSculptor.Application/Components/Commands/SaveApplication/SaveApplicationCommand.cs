using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Application.Components.Commands.SaveApplication;

[ModelCommand("SaveApplication")]
[ModelChange]
[RequiresPermission(Permission.ModelEdit)]
[Audited(AuditActions.ApplicationUpdated, ObjectType.Application)]
public sealed record SaveApplicationCommand(
    Guid ProjectId,
    Guid ComponentId,
    Guid ResourceId,
    ApplicationDefinition Definition,
    int ExpectedVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
