using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Projects.Commands.DeleteProject;

[ModelCommand("DeleteProject")]
[ModelChange]
[RequiresPermission(Permission.ProjectAdminister)]
public sealed record DeleteProjectCommand(Guid ProjectId, string NameConfirmation, int ExpectedVersion)
    : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
