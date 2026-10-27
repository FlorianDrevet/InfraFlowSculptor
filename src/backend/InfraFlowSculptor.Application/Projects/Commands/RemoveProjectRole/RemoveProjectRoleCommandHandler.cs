using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.RemoveProjectRole;

public sealed class RemoveProjectRoleCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization)
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

        if (project.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The project changed. Reload it before removing a role.");
        }

        var memberId = new UserProfileId(command.MemberId);
        var removed = project.RemoveRole(memberId, command.Role, command.Scope);
        if (removed.IsError)
        {
            return removed.Errors;
        }

        return new ProjectRoleAssignmentSummary(
            command.MemberId,
            command.Role,
            command.Scope,
            project.Version,
            ProjectRoleSources.Direct);
    }
}
