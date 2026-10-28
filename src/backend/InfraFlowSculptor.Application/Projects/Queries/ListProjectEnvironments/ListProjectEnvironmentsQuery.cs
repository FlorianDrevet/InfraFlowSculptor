using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Queries.ListProjectEnvironments;

[RequiresPermission(Permission.ProjectRead)]
public sealed record ListProjectEnvironmentsQuery(Guid ProjectId)
    : IQuery<ErrorOr<IReadOnlyList<ProjectEnvironmentSummary>>>, IProjectScopedRequest;
