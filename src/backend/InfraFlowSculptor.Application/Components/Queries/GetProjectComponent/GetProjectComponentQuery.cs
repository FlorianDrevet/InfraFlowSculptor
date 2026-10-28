using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetProjectComponent;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetProjectComponentQuery(Guid ProjectId, Guid ComponentId)
    : IQuery<ErrorOr<ComponentSummary>>, IProjectScopedRequest;
