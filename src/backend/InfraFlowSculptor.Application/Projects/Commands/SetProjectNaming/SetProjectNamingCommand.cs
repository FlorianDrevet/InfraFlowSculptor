using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Naming;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Projects.Commands.SetProjectNaming;

[ModelCommand("SetProjectNaming")]
[ModelChange]
[RequiresPermission(Permission.ConventionsManage)]
[Audited(AuditActions.ProjectNamingUpdated, ObjectType.Project)]
public sealed record SetProjectNamingCommand(
    Guid ProjectId,
    string Preset,
    string DefaultTemplate,
    string ResourceGroupTemplate,
    IReadOnlyList<NamingOverrideValue> TypeOverrides,
    int ExpectedVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
