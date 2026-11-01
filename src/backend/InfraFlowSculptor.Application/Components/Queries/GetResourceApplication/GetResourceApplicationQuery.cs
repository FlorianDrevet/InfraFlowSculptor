using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceApplication;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetResourceApplicationQuery(Guid ProjectId, Guid ComponentId, Guid ResourceId)
    : IQuery<ErrorOr<ResourceApplicationSummary>>, IProjectScopedRequest;
