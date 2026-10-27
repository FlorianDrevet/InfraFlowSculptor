using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.DeleteProjectEnvironment;

public sealed class DeleteProjectEnvironmentCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization,
    IProjectAuthorizer projectAuthorizer)
    : ICommandHandler<DeleteProjectEnvironmentCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        DeleteProjectEnvironmentCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var projectId = new ProjectId(command.ProjectId);
        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.Environments)
                .ThenInclude(environment => environment.Tags)
            .SingleOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
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
            var authorization = await projectAuthorizer.GetProjectAuthorizationContextAsync(
                currentUser.Key,
                organizationId,
                projectId,
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
            return Error.Conflict("CONFLICT_VERSION", "The project changed. Reload it before deleting an environment.",
                new Dictionary<string, object> { ["currentVersion"] = project.Version });
        }

        if (!command.ConfirmDeletion)
        {
            return Error.Conflict(
                "ENVIRONMENT_DELETE_CONFIRMATION_REQUIRED",
                "Review the affected environment data and confirm deletion. No components or resources are modeled in J0-07.",
                new Dictionary<string, object>
                {
                    ["affectedData"] = new Dictionary<string, object>
                    {
                        ["environmentId"] = environment.Id,
                        ["name"] = environment.Name,
                        ["tags"] = environment.Tags.ToDictionary(tag => tag.Key, tag => tag.Value),
                        ["approvers"] = environment.Approvers,
                        ["runnerOverrides"] = environment.Runners
                    },
                    ["affectedResources"] = Array.Empty<string>()
                });
        }

        var deleted = project.RemoveEnvironment(command.EnvironmentId);
        return deleted.IsError
            ? deleted.Errors
            : new ModelCommandOutcome($"Environment {command.EnvironmentId:D} deleted.", []);
    }
}
