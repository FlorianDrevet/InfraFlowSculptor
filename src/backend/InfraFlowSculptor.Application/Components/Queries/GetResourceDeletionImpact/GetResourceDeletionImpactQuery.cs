using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceDeletionImpact;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetResourceDeletionImpactQuery(Guid ProjectId, Guid ComponentId, Guid ResourceId)
    : IQuery<ErrorOr<ResourceDeletionImpactSummary>>, IProjectScopedRequest;
