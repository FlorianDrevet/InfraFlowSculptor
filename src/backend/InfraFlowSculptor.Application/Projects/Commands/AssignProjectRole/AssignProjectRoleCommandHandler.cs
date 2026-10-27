using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.AssignProjectRole;

public sealed class AssignProjectRoleCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization,
    IProjectAuthorizer projectAuthorizer)
    : ICommandHandler<AssignProjectRoleCommand, ErrorOr<ProjectRoleAssignmentSummary>>
{
    public async ValueTask<ErrorOr<ProjectRoleAssignmentSummary>> Handle(
        AssignProjectRoleCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var projectId = new ProjectId(command.ProjectId);
        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.RoleAssignments)
            .SingleOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        if (project.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The project changed. Reload it before assigning a role.");
        }

        var memberId = new UserProfileId(command.MemberId);
        var organization = await dbContext.Query<OrganizationAggregate>()
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);
        if (organization is null || project.OrganizationId != organizationId
            || organization.Members.All(member => member.UserProfileId != memberId))
        {
            return Error.NotFound("NOT_FOUND", "The organization member was not found.");
        }

        var authorization = await projectAuthorizer.GetProjectAuthorizationContextAsync(
            currentUser.Key,
            organizationId,
            projectId,
            cancellationToken);
        if (authorization is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var missingPermission = ProjectRoles.MissingPermissionToAssign(
            authorization.Permissions,
            authorization.IsOwner,
            command.Role);
        if (missingPermission is not null)
        {
            return Error.Forbidden(
                "RG-ORG-23",
                $"The permission '{missingPermission}' is required to assign this role.",
                new Dictionary<string, object> { ["permission"] = missingPermission });
        }

        var assigned = project.AssignRole(memberId, command.Role, command.Scope);
        if (assigned.IsError)
        {
            return assigned.Errors;
        }

        organization.RecordProjectAuthorizationChange();

        return new ProjectRoleAssignmentSummary(
            command.MemberId,
            command.Role,
            command.Scope,
            project.Version,
            ProjectRoleSources.Direct);
    }
}
