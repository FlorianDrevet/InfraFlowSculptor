using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Queries.GetProject;

public sealed class GetProjectQueryHandler(IIfsDbContext dbContext, ICurrentUser currentUser)
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

        var userProfileId = await dbContext.Query<UserProfile>()
            .Where(profile => profile.TenantId == currentUser.Key.TenantId
                && profile.ObjectId == currentUser.Key.ObjectId)
            .Select(profile => (Guid?)profile.Id.Value)
            .SingleOrDefaultAsync(cancellationToken);
        var isFavorite = userProfileId is not null
            && await dbContext.Query<UserProjectFavorite>()
                .AnyAsync(favorite => favorite.UserProfileId == new UserProfileId(userProfileId.Value)
                    && favorite.ProjectId == project.Id,
                    cancellationToken);
        var componentCount = await dbContext.Query<ComponentAggregate>()
            .CountAsync(candidate => candidate.ProjectId == project.Id, cancellationToken);

        return new ProjectSummary(
            project.Id.Value,
            project.Version,
            project.Name,
            project.Code,
            project.Description,
            project.ModelVersion,
            project.Environments.Count,
            componentCount,
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
            project.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase),
            isFavorite);
    }
}
