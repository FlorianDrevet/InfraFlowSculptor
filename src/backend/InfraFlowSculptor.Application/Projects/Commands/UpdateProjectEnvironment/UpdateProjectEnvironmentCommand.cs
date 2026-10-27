using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Projects.Commands.UpdateProjectEnvironment;

[ModelCommand("UpdateProjectEnvironment")]
[ModelChange]
[RequiresPermission(Permission.EnvironmentsManage)]
public sealed record UpdateProjectEnvironmentCommand(
    Guid ProjectId,
    Guid EnvironmentId,
    string Name,
    string Code,
    Guid SubscriptionId,
    string Region,
    bool IsProtected,
    IReadOnlyList<string> Approvers,
    IReadOnlyList<string> Runners,
    string Description,
    IReadOnlyDictionary<string, string> Tags,
    bool ConfirmTargetImpact,
    int ExpectedVersion,
    string? DeploymentConnection = null) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
