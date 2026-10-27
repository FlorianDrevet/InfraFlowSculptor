using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.RemoveProjectRole;

public sealed class RemoveProjectRoleCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization,
    IProjectAuthorizer projectAuthorizer)
    : ICommandHandler<RemoveProjectRoleCommand, ErrorOr<ProjectRoleAssignmentSummary>>
{
    public async ValueTask<ErrorOr<ProjectRoleAssignmentSummary>> Handle(
        RemoveProjectRoleCommand command,
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

        var organization = await dbContext.Query<OrganizationAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);
        if (organization is null || project.OrganizationId != organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        if (project.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The project changed. Reload it before removing a role.");
        }

        var authorization = await projectAuthorizer.GetProjectAuthorizationContextAsync(
            currentUser.Key,
            organizationId,
            project.Id,
            cancellationToken);
        if (authorization is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var missingPermission = command.Role == ProjectRole.Owner
            ? ProjectRoles.MissingPermissionToAssign(authorization.Permissions, authorization.IsOwner, command.Role)
            : null;
        if (missingPermission is not null)
        {
            return Error.Forbidden(
                "RG-ORG-23",
                $"The permission '{missingPermission}' is required to remove this role.",
                new Dictionary<string, object> { ["permission"] = missingPermission });
        }

        var memberId = new UserProfileId(command.MemberId);
        var removed = project.RemoveRole(memberId, command.Role, command.Scope);
        if (removed.IsError)
        {
            return removed.Errors;
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
