using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.CreateResourceGroup;

public sealed class CreateResourceGroupCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider)
    : ICommandHandler<CreateResourceGroupCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        CreateResourceGroupCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var projectId = new ProjectId(command.ProjectId);
        var component = await dbContext.Query<ComponentAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == new ComponentId(command.ComponentId)
                && candidate.ProjectId == projectId,
                cancellationToken);
        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.Environments)
            .SingleOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (component is null || project is null || project.OrganizationId != organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The component was not found.");
        }

        if (component.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The component changed. Reload it before adding a resource group.",
                new Dictionary<string, object> { ["currentVersion"] = component.Version });
        }

        var valid = ResourceGroupCommandSupport.Validate(
            component,
            project,
            command.Region,
            command.ForcedNames,
            catalogProvider);
        if (valid.IsError)
        {
            return valid.Errors;
        }

        var group = component.AddResourceGroup(
            command.ResourceGroupId,
            command.Name,
            command.Region,
            command.Tags,
            command.ForcedNames);
        return group.IsError
            ? group.Errors
            : new ModelCommandOutcome($"Resource group {group.Value.Name} created.", []);
    }
}
