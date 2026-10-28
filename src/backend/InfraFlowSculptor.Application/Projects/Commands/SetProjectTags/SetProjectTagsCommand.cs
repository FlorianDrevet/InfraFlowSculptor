using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Projects.Commands.SetProjectTags;

[ModelCommand("SetProjectTags")]
[ModelChange]
[RequiresPermission(Permission.ConventionsManage)]
public sealed record SetProjectTagsCommand(Guid ProjectId, IReadOnlyDictionary<string, string> Tags, int ExpectedVersion)
    : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
