using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Projects.Commands.UpdateProjectSettings;

[ModelCommand("UpdateProjectSettings")]
[ModelChange]
[RequiresPermission(Permission.ProjectAdminister)]
public sealed record UpdateProjectSettingsCommand(
    Guid ProjectId,
    string Name,
    string Code,
    string Description,
    bool SystemTagsEnabled,
    Guid? CoordinatorUserProfileId,
    IReadOnlyDictionary<string, string> Tags,
    bool ConfirmImpact,
    int ExpectedVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
