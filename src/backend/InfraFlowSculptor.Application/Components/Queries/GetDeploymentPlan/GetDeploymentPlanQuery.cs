using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Engine.Plan;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetDeploymentPlan;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetDeploymentPlanQuery(Guid ProjectId, string? Component, string? Target)
    : IQuery<ErrorOr<DeploymentPlan>>, IProjectScopedRequest;
