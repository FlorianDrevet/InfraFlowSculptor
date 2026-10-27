using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.MoveProjectEnvironment;

public sealed class MoveProjectEnvironmentCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization,
    IProjectAuthorizer projectAuthorizer)
    : ICommandHandler<MoveProjectEnvironmentCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        MoveProjectEnvironmentCommand command,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.Environments)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(command.ProjectId), cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var environment = project.Environments.SingleOrDefault(candidate => candidate.Id == command.EnvironmentId);
        if (environment is null)
        {
            return Error.NotFound("ENVIRONMENT_NOT_FOUND", "The environment was not found.");
        }

        if (environment.IsProtected)
        {
            if (currentOrganization.Id is not { } organizationId)
            {
                return Error.NotFound("NOT_FOUND", "The project was not found.");
            }

            var authorization = await projectAuthorizer.GetProjectAuthorizationContextAsync(
                currentUser.Key,
                organizationId,
                new ProjectId(command.ProjectId),
                cancellationToken);
            if (authorization is null)
            {
                return Error.NotFound("NOT_FOUND", "The project was not found.");
            }

            if (!authorization.Permissions.Contains(Permission.ProtectedEnvironmentsManage))
            {
                return Error.Forbidden(
                    "PROJECT_PERMISSION_REQUIRED",
                    $"The project permission '{Permission.ProtectedEnvironmentsManage}' is required.",
                    new Dictionary<string, object> { ["permission"] = Permission.ProtectedEnvironmentsManage });
            }
        }

        if (project.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The project changed. Reload it before moving an environment.",
                new Dictionary<string, object> { ["currentVersion"] = project.Version });
        }

        var moved = project.ReorderEnvironment(command.EnvironmentId, command.Position);
        return moved.IsError
            ? moved.Errors
            : new ModelCommandOutcome($"Environment {command.EnvironmentId:D} moved to position {command.Position}.", []);
    }
}
