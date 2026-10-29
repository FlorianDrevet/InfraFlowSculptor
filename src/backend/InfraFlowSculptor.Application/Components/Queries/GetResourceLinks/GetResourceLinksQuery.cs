using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceLinks;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetResourceLinksQuery(Guid ProjectId, Guid ComponentId, Guid ResourceId)
    : IQuery<ErrorOr<ResourceLinksSummary>>, IProjectScopedRequest;
