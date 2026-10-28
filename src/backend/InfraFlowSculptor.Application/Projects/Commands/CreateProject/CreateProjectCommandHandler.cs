using System.Diagnostics.CodeAnalysis;
using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Plans;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.CreateProject;

[SuppressMessage("Performance", "CA1862", Justification = "EF Core translates lower() into PostgreSQL for the case-insensitive uniqueness check.")]
public sealed class CreateProjectCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICurrentUser currentUser,
    IPlanLimitGuard planLimitGuard,
    ICatalogProvider catalogProvider)
    : ICommandHandler<CreateProjectCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        CreateProjectCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var limit = planLimitGuard.EnsureCanCreate(organizationId, LimitKind.Project);
        if (limit.IsError)
        {
            return limit.Errors;
        }

        var catalogVersion = catalogProvider.GetVersion(command.CatalogVersion);
        if (catalogVersion is null || !string.Equals(catalogVersion.Status, "published", StringComparison.Ordinal))
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_INVALID", "The project must pin a published catalog version.");
        }

        var duplicate = await dbContext.Query<ProjectAggregate>()
            .IgnoreQueryFilters()
            .AnyAsync(project => project.OrganizationId == organizationId
                && (project.Name.ToLower() == command.Name.Trim().ToLower()
                    || project.Code.ToLower() == command.Code.Trim().ToLower()),
                cancellationToken);
        if (duplicate)
        {
            return Error.Conflict("PROJECT_NAME_OR_CODE_DUPLICATE", "A project with this name or code already exists in the organization.");
        }

        var creator = await dbContext.Query<UserProfile>()
            .SingleOrDefaultAsync(profile => profile.TenantId == currentUser.Key.TenantId
                && profile.ObjectId == currentUser.Key.ObjectId, cancellationToken);
        if (creator is null)
        {
            return Error.NotFound("NOT_FOUND", "The current user profile was not found.");
        }

        var project = ProjectAggregate.Create(
            new ProjectId(command.ProjectId),
            organizationId,
            command.Name,
            command.Code,
            command.TenantId,
            command.Description,
            catalogVersion.Version,
            creator.Id,
            command.CoordinatorUserProfileId is { } coordinatorId ? new UserProfileId(coordinatorId) : null,
            command.DefaultRunners,
            command.DefaultReviewers);
        if (project.IsError)
        {
            return project.Errors;
        }

        dbContext.Query<ProjectAggregate>().Add(project.Value);
        return new ModelCommandOutcome($"Project {project.Value.Id.Value:D} created.", []);
    }
}
