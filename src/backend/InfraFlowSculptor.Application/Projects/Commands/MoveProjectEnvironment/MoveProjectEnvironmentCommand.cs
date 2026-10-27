using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Projects.Commands.MoveProjectEnvironment;

[ModelCommand("MoveProjectEnvironment")]
[ModelChange]
[RequiresPermission(Permission.EnvironmentsManage)]
public sealed record MoveProjectEnvironmentCommand(Guid ProjectId, Guid EnvironmentId, int Position, int ExpectedVersion)
    : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
