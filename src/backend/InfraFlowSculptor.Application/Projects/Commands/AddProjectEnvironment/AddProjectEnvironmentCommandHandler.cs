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

namespace InfraFlowSculptor.Application.Projects.Commands.AddProjectEnvironment;

using Component = InfraFlowSculptor.Domain.ComponentAggregate.ComponentAggregate;

public sealed class AddProjectEnvironmentCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization,
    IProjectAuthorizer projectAuthorizer,
    ICatalogProvider catalogProvider)
    : ICommandHandler<AddProjectEnvironmentCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        AddProjectEnvironmentCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var projectId = new ProjectId(command.ProjectId);
        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.Environments)
            .SingleOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
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

        if (command.IsProtected && !authorization.Permissions.Contains(Permission.ProtectedEnvironmentsManage))
        {
            return Error.Forbidden(
                "PROJECT_PERMISSION_REQUIRED",
                $"The project permission '{Permission.ProtectedEnvironmentsManage}' is required.",
                new Dictionary<string, object> { ["permission"] = Permission.ProtectedEnvironmentsManage });
        }

        if (project.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The project changed. Reload it before adding an environment.",
                new Dictionary<string, object> { ["currentVersion"] = project.Version });
        }

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

        var catalogVersion = catalogProvider.GetVersion(project.CatalogVersion);
        if (catalogVersion is null || !catalogVersion.Regions.Any(region =>
                string.Equals(region.Name, command.Region.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Validation("ENVIRONMENT_REGION_INVALID", "The selected region is not present in the project's pinned catalog.");
        }

        var created = project.AddEnvironment(
            command.EnvironmentId,
            command.Name,
            command.Code,
            command.SubscriptionId,
            command.Region,
            command.Position,
            command.IsProtected,
            command.Approvers,
            command.Tags,
            command.Description,
            command.Runners,
            command.DeploymentConnection);
        if (created.IsError)
        {
            return created.Errors;
        }

        var components = await dbContext.Query<Component>()
            .Where(component => component.ProjectId == projectId)
            .ToArrayAsync(cancellationToken);
        foreach (var component in components)
        {
            component.AddResourceTargetEnvironment(created.Value.Id);
        }

        return new ModelCommandOutcome($"Environment {created.Value.Code} added at position {created.Value.Position}.", []);
    }
}
