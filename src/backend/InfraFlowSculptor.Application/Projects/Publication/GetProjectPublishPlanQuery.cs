using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Publication;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetProjectPublishPlanQuery(Guid ProjectId)
    : IQuery<ErrorOr<ProjectPublishPlanView>>, IProjectScopedRequest;
