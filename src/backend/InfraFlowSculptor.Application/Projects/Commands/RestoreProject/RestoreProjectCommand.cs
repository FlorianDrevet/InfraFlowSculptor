using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Projects.Commands.RestoreProject;

[ModelCommand("RestoreProject")]
[ModelChange]
[RequiresPermission(Permission.ProjectAdminister)]
public sealed record RestoreProjectCommand(Guid ProjectId, int ExpectedVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
