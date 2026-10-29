using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceLinkOptions;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetResourceLinkOptionsQuery(Guid ProjectId, Guid ComponentId, Guid ResourceId)
    : IQuery<ErrorOr<ResourceLinkOptionsSummary>>, IProjectScopedRequest;
