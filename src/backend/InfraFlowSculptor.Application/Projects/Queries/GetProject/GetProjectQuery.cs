using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Queries.GetProject;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetProjectQuery(Guid ProjectId) : IQuery<ErrorOr<ProjectSummary>>, IProjectScopedRequest;
