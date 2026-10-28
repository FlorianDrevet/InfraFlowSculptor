using ErrorOr;
using InfraFlowSculptor.Application.Projects;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Queries.ListProjects;

public sealed record ListProjectsQuery(bool IncludeDeleted = false) : IQuery<ErrorOr<IReadOnlyList<ProjectSummary>>>;
