using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ComponentAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.CreateComponent;

[ModelCommand("CreateComponent")]
[ModelChange]
[RequiresPermission(Permission.ComponentsManage)]
[Audited(AuditActions.ComponentCreated, ObjectType.Component)]
public sealed record CreateComponentCommand(
    Guid ProjectId,
    Guid ComponentId,
    string Name,
    string Code,
    string Description,
    ComponentDeploymentMode Mode,
    bool TargetsAllEnvironments,
    IReadOnlyList<Guid> TargetEnvironmentIds,
    string? SingleTargetCode,
    Guid? SingleTargetSubscriptionId,
    string? SingleTargetRegion,
    string? SingleTargetDeploymentConnection,
    bool? SingleTargetIsProtected,
    IReadOnlyList<string>? SingleTargetApprovers,
    IReadOnlyList<string>? SingleTargetRunners,
    RemovedResourceBehavior RemovedResourceBehavior,
    bool DeletionProtectionEnabled,
    IReadOnlyDictionary<string, string> Tags,
    int ExpectedVersion) : IModelCommand, IProjectScopedRequest
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
