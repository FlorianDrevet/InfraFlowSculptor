using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Projects.Commands.DeleteProjectEnvironment;

[ModelCommand("DeleteProjectEnvironment")]
[ModelChange]
[RequiresPermission(Permission.EnvironmentsManage)]
public sealed record DeleteProjectEnvironmentCommand(Guid ProjectId, Guid EnvironmentId, bool ConfirmDeletion, int ExpectedVersion)
    : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
