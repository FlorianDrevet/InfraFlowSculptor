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

namespace InfraFlowSculptor.Application.Components.Commands.UpdateComponent;

public sealed class UpdateComponentCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider)
    : ICommandHandler<UpdateComponentCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        UpdateComponentCommand command,
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
        var component = await dbContext.Query<ComponentAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == new ComponentId(command.ComponentId)
                && candidate.ProjectId == projectId,
                cancellationToken);
        if (project is null || component is null || project.OrganizationId != organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The component was not found.");
        }

        if (component.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The component changed. Reload it before saving.",
                new Dictionary<string, object> { ["currentVersion"] = component.Version });
        }

        var targetResult = CreateSingleTarget(command, project, catalogProvider, component.SingleTarget?.Id);
        if (targetResult.IsError)
        {
            return targetResult.Errors;
        }

        var existingComponents = await dbContext.Query<ComponentAggregate>()
            .Where(candidate => candidate.ProjectId == projectId)
            .ToArrayAsync(cancellationToken);
        var updated = component.Update(
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
        if (updated.IsError)
        {
            return updated.Errors;
        }

        var tags = component.SetTags(command.Tags);
        if (tags.IsError)
        {
            return tags.Errors;
        }

        return new ModelCommandOutcome($"Component {component.Code} updated.", []);
    }

    private static ErrorOr<ComponentSingleTarget?> CreateSingleTarget(
        UpdateComponentCommand command,
        ProjectAggregate project,
        ICatalogProvider catalogProvider,
        Guid? targetId)
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
            project.DefaultRunners,
            targetId);
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
