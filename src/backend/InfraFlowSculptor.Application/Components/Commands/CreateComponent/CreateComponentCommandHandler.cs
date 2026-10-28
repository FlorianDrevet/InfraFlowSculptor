using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.CreateComponent;

public sealed class CreateComponentCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider)
    : ICommandHandler<CreateComponentCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        CreateComponentCommand command,
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

        if (project.OrganizationId != organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        if (project.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The project changed. Reload it before creating a component.",
                new Dictionary<string, object> { ["currentVersion"] = project.Version });
        }

        var targetResult = CreateSingleTarget(command, project, catalogProvider);
        if (targetResult.IsError)
        {
            return targetResult.Errors;
        }

        var existingComponents = await dbContext.Query<ComponentAggregate>()
            .Where(component => component.ProjectId == projectId)
            .ToArrayAsync(cancellationToken);
        var component = ComponentAggregate.Create(
            new ComponentId(command.ComponentId),
            projectId,
            organizationId,
            command.Name,
            command.Code,
            command.Description,
            command.Mode,
            project.Environments,
            command.TargetsAllEnvironments,
            command.TargetEnvironmentIds,
            targetResult.Value,
            command.RemovedResourceBehavior,
            command.DeletionProtectionEnabled,
            existingComponents);
        if (component.IsError)
        {
            return component.Errors;
        }

        var tags = component.Value.SetTags(command.Tags);
        if (tags.IsError)
        {
            return tags.Errors;
        }

        dbContext.Query<ComponentAggregate>().Add(component.Value);
        return new ModelCommandOutcome($"Component {component.Value.Code} created.", []);
    }

    private static ErrorOr<ComponentSingleTarget?> CreateSingleTarget(
        CreateComponentCommand command,
        ProjectAggregate project,
        ICatalogProvider catalogProvider)
    {
        if (command.Mode != ComponentDeploymentMode.Single)
        {
            return (ComponentSingleTarget?)null;
        }

        var target = ComponentSingleTarget.Create(
            project.Code,
            command.SingleTargetCode,
            command.SingleTargetSubscriptionId ?? Guid.Empty,
            command.SingleTargetRegion ?? string.Empty,
            command.SingleTargetDeploymentConnection,
            command.SingleTargetIsProtected,
            command.SingleTargetApprovers,
            command.SingleTargetRunners,
            project.Environments,
            project.DefaultRunners);
        if (target.IsError)
        {
            return target.Errors;
        }

        var catalog = catalogProvider.GetVersion(project.CatalogVersion);
        if (catalog is null || !catalog.Regions.Any(region =>
                string.Equals(region.Name, target.Value.Region, StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Validation("COMPONENT_SINGLE_TARGET_REGION_INVALID", "The selected region is not present in the project's pinned catalog.");
        }

        return target.Value;
    }
}
