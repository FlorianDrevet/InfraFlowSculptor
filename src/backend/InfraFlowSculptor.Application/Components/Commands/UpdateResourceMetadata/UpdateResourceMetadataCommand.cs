using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;

namespace InfraFlowSculptor.Application.Components.Commands.UpdateResourceMetadata;

[ModelCommand("UpdateResourceMetadata")]
[ModelChange]
[RequiresPermission(Permission.ModelEdit)]
[Audited(AuditActions.ResourceUpdated, ObjectType.Resource)]
public sealed record UpdateResourceMetadataCommand(
    Guid ProjectId,
    Guid ComponentId,
    Guid ResourceId,
    string Description,
    IReadOnlyDictionary<string, string> Tags,
    int ExpectedVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
