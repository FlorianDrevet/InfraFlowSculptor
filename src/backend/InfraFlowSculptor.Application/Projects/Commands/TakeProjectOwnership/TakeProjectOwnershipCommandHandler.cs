using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.TakeProjectOwnership;

public sealed class TakeProjectOwnershipCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization)
    : ICommandHandler<TakeProjectOwnershipCommand, ErrorOr<ProjectRoleAssignmentSummary>>
{
    public async ValueTask<ErrorOr<ProjectRoleAssignmentSummary>> Handle(
        TakeProjectOwnershipCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.RoleAssignments)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(command.ProjectId), cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        if (project.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The project changed. Reload it before taking ownership.");
        }

        var actor = await dbContext.Query<UserProfile>()
            .SingleOrDefaultAsync(profile => profile.TenantId == currentUser.Key.TenantId
                && profile.ObjectId == currentUser.Key.ObjectId,
                cancellationToken);
        if (actor is null)
        {
            return Error.NotFound("NOT_FOUND", "The organization member was not found.");
        }

        var organization = await dbContext.Query<OrganizationAggregate>()
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);
        if (organization is null || project.OrganizationId != organizationId
            || organization.Members.All(member => member.UserProfileId != actor.Id))
        {
            return Error.NotFound("NOT_FOUND", "The organization member was not found.");
        }

        var alreadyOwner = project.HasRole(actor.Id, ProjectRole.Owner);
        var ownership = project.TakeOwnership(actor.Id);
        if (ownership.IsError)
        {
            return ownership.Errors;
        }

        if (!alreadyOwner)
        {
            organization.RecordProjectAuthorizationChange();
        }

        return new ProjectRoleAssignmentSummary(
            actor.Id.Value,
            ProjectRole.Owner,
            ProjectRoleScope.Project,
            project.Version,
            ProjectRoleSources.Direct);
    }
}
