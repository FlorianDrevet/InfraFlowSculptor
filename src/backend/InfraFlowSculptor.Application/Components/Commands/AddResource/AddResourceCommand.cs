using System.Text.Json;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Components.Commands.AddResource;

[ModelCommand("AddResource")]
[ModelChange]
[RequiresPermission(Permission.ModelEdit)]
[Audited(AuditActions.ResourceCreated, ObjectType.Resource)]
public sealed record AddResourceCommand(
    Guid ProjectId,
    Guid ComponentId,
    Guid ResourceId,
    Guid ResourceGroupId,
    string Type,
    string LogicalName,
    string Description,
    bool IsExisting,
    JsonElement Properties,
    IReadOnlyDictionary<string, string> Tags,
    int ExpectedVersion) : IModelCommand, IProjectScopedRequest
{
    public Guid? DefaultIdentityResourceId { get; init; }

    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
