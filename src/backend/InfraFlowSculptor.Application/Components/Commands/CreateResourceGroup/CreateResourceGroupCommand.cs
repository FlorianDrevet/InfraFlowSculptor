using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.CreateResourceGroup;

[ModelCommand("CreateResourceGroup")]
[ModelChange]
[RequiresPermission(Permission.ModelEdit)]
[Audited(AuditActions.ResourceGroupCreated, ObjectType.ResourceGroup)]
public sealed record CreateResourceGroupCommand(
    Guid ProjectId,
    Guid ComponentId,
    Guid ResourceGroupId,
    string Name,
    string? Region,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyDictionary<Guid, string> ForcedNames,
    int ExpectedVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
