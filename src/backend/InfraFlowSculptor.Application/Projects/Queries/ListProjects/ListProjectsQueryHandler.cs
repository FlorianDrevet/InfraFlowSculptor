using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Queries.ListProjects;

public sealed class ListProjectsQueryHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICurrentUser currentUser,
    IOrganizationRoleAuthorizer organizationRoleAuthorizer)
    : IQueryHandler<ListProjectsQuery, ErrorOr<IReadOnlyList<ProjectSummary>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<ProjectSummary>>> Handle(
        ListProjectsQuery query,
        CancellationToken cancellationToken)
    {
        _ = query;
        if (currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var profileId = await dbContext.Query<UserProfile>()
            .Where(profile => profile.TenantId == currentUser.Key.TenantId
                && profile.ObjectId == currentUser.Key.ObjectId)
            .Select(profile => (Guid?)profile.Id.Value)
            .SingleOrDefaultAsync(cancellationToken);
        if (profileId is null)
        {
            return Error.NotFound("NOT_FOUND", "The current user profile was not found.");
        }

        var seesAllProjects = await organizationRoleAuthorizer.HasRoleAsync(
                currentUser.Key,
                organizationId,
                OrganizationRole.Administrator,
                cancellationToken)
            || await organizationRoleAuthorizer.HasRoleAsync(
                currentUser.Key,
                organizationId,
                OrganizationRole.Auditor,
                cancellationToken);

        var projects = dbContext.Query<ProjectAggregate>()
            .AsNoTracking()
            .Include(project => project.RoleAssignments)
            .Include(project => project.Environments)
            .Include(project => project.Tags)
            .Where(project => project.DeletedAt == null);
        if (!seesAllProjects)
        {
            projects = projects.Where(project => project.RoleAssignments
                .Any(assignment => assignment.Principal == new UserProfileId(profileId.Value)));
        }

        var visibleProjects = await projects
            .OrderBy(project => project.Name)
            .ToArrayAsync(cancellationToken);
        var summaries = visibleProjects.Select(project => new ProjectSummary(
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
                project.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase)))
            .ToArray();
        return summaries;
    }
}
