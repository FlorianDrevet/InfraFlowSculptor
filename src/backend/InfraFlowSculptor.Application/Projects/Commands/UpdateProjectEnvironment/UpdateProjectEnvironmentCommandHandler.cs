using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.UpdateProjectEnvironment;

using Component = InfraFlowSculptor.Domain.ComponentAggregate.ComponentAggregate;

public sealed class UpdateProjectEnvironmentCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization,
    IProjectAuthorizer projectAuthorizer,
    ICatalogProvider catalogProvider)
    : ICommandHandler<UpdateProjectEnvironmentCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        UpdateProjectEnvironmentCommand command,
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

        var authorization = await projectAuthorizer.GetProjectAuthorizationContextAsync(
            currentUser.Key,
            organizationId,
            projectId,
            cancellationToken);
        if (authorization is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        if ((environment.IsProtected || command.IsProtected)
            && !authorization.Permissions.Contains(Permission.ProtectedEnvironmentsManage))
        {
            return Error.Forbidden(
                "PROJECT_PERMISSION_REQUIRED",
                $"The project permission '{Permission.ProtectedEnvironmentsManage}' is required.",
                new Dictionary<string, object> { ["permission"] = Permission.ProtectedEnvironmentsManage });
        }

        if (project.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The project changed. Reload it before updating the environment.",
                new Dictionary<string, object> { ["currentVersion"] = project.Version });
        }

        if (!string.Equals(environment.Code, command.Code.Trim(), StringComparison.Ordinal))
        {
            var targetCodeConflict = await dbContext.Query<Component>()
                .AnyAsync(component => component.ProjectId == projectId
                    && component.Mode == ComponentDeploymentMode.Single
                    && component.SingleTarget != null
                    && component.SingleTarget.Code == command.Code.Trim(),
                    cancellationToken);
            if (targetCodeConflict)
            {
                return Error.Conflict(
                    "COMPONENT_SINGLE_TARGET_ENVIRONMENT_CODE_CONFLICT",
                    "An environment code cannot match a Single component target code.");
            }
        }

        var catalogVersion = catalogProvider.GetVersion(project.CatalogVersion);
        if (catalogVersion is null || !catalogVersion.Regions.Any(region =>
                string.Equals(region.Name, command.Region.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Validation("ENVIRONMENT_REGION_INVALID", "The selected region is not present in the project's pinned catalog.");
        }

        var updated = project.UpdateEnvironment(
            command.EnvironmentId,
            command.Name,
            command.Code,
            command.SubscriptionId,
            command.Region,
            command.IsProtected,
            command.Approvers,
            command.Runners,
            command.Description,
            command.Tags,
            command.ConfirmTargetImpact,
            command.DeploymentConnection);
        return updated.IsError
            ? updated.Errors
            : new ModelCommandOutcome($"Environment {command.EnvironmentId:D} updated.", []);
    }
}
