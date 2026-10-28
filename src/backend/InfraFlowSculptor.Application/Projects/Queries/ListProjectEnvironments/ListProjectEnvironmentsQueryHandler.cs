using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Queries.ListProjectEnvironments;

public sealed class ListProjectEnvironmentsQueryHandler(IIfsDbContext dbContext)
    : IQueryHandler<ListProjectEnvironmentsQuery, ErrorOr<IReadOnlyList<ProjectEnvironmentSummary>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<ProjectEnvironmentSummary>>> Handle(
        ListProjectEnvironmentsQuery query,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Query<ProjectAggregate>()
            .AsNoTracking()
            .Include(candidate => candidate.Environments)
                .ThenInclude(environment => environment.Tags)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(query.ProjectId), cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var environments = project.Environments
            .OrderBy(environment => environment.Position)
            .Select(environment => new ProjectEnvironmentSummary(
                environment.Id,
                environment.Name,
                environment.Code,
                environment.Position,
                environment.SubscriptionId,
                environment.Region,
                environment.DeploymentConnection,
                environment.IsProtected,
                environment.Approvers,
                environment.Runners,
                environment.Description,
                environment.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase),
                environment.FirstPublishedAt))
            .ToArray();
        return environments;
    }
}
