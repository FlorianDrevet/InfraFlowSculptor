using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.DeleteProjectEnvironment;

using Component = InfraFlowSculptor.Domain.ComponentAggregate.ComponentAggregate;

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

        var targetedComponents = await dbContext.Query<Component>()
            .Where(component => component.ProjectId == projectId
                && component.Mode == ComponentDeploymentMode.PerEnvironment
                && !component.TargetsAllEnvironments
                && component.TargetEnvironmentIds.Contains(command.EnvironmentId))
            .ToArrayAsync(cancellationToken);
        if (targetedComponents.Any(component => component.WouldLoseLastTargetAfterRemoving(command.EnvironmentId)))
        {
            return Error.Conflict(
                "COMPONENT_TARGET_REQUIRED",
                "The environment cannot be deleted while it is the only target of a PerEnvironment component.");
        }

        if (!command.ConfirmDeletion)
        {
            return Error.Conflict(
                "ENVIRONMENT_DELETE_CONFIRMATION_REQUIRED",
                "Review the affected environment and component targets, then confirm deletion.",
                new Dictionary<string, object>
                {
                    ["affectedData"] = new Dictionary<string, object>
                    {
                        ["environmentId"] = environment.Id,
                        ["name"] = environment.Name,
                        ["tags"] = environment.Tags.ToDictionary(tag => tag.Key, tag => tag.Value),
                        ["approvers"] = environment.Approvers,
                        ["runnerOverrides"] = environment.Runners,
                        ["components"] = targetedComponents.Select(component => new
                        {
                            id = component.Id.Value,
                            component.Name,
                            component.Code
                        }).ToArray()
                    },
                    ["affectedResources"] = Array.Empty<string>()
                });
        }

        foreach (var component in targetedComponents)
        {
            var targetRemoved = component.RemoveTargetEnvironment(command.EnvironmentId);
            if (targetRemoved.IsError)
            {
                return targetRemoved.Errors;
            }
        }

        var deleted = project.RemoveEnvironment(command.EnvironmentId);
        return deleted.IsError
            ? deleted.Errors
            : new ModelCommandOutcome($"Environment {command.EnvironmentId:D} deleted.", []);
    }
}
