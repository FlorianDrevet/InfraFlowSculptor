using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetLinkRemovalImpact;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetLinkRemovalImpactQuery(Guid ProjectId, Guid ComponentId, Guid ResourceId, Guid LinkId)
    : IQuery<ErrorOr<LinkImpactSummary>>, IProjectScopedRequest;
