using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.ListProjectComponents;

[RequiresPermission(Permission.ProjectRead)]
public sealed record ListProjectComponentsQuery(Guid ProjectId)
    : IQuery<ErrorOr<IReadOnlyList<ComponentSummary>>>, IProjectScopedRequest;
