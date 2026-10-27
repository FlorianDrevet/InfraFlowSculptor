using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Queries.GetProject;

public sealed class GetProjectQueryHandler(IIfsDbContext dbContext)
    : IQueryHandler<GetProjectQuery, ErrorOr<ProjectSummary>>
{
    public async ValueTask<ErrorOr<ProjectSummary>> Handle(
        GetProjectQuery query,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Query<ProjectAggregate>()
            .AsNoTracking()
            .Include(candidate => candidate.Environments)
            .Include(candidate => candidate.Tags)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(query.ProjectId), cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        return new ProjectSummary(
            project.Id.Value,
            project.Version,
            project.Name,
            project.Code,
            project.Description,
            project.ModelVersion,
            project.Environments.Count,
            project.FirstPublishedAt,
            project.DeletedAt,
            project.TenantId,
            project.InfrastructureLanguage,
            project.CiPlatform,
            project.ModuleSource,
            project.CatalogVersion,
            project.SystemTagsEnabled,
            project.CoordinatorUserProfileId?.Value,
            project.DefaultRunners,
            project.DefaultReviewers,
            project.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase));
    }
}
