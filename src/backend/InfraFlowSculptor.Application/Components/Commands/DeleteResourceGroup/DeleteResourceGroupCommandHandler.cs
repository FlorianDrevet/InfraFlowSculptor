using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.DeleteResourceGroup;

public sealed class DeleteResourceGroupCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization)
    : ICommandHandler<DeleteResourceGroupCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        DeleteResourceGroupCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var projectId = new ProjectId(command.ProjectId);
        var component = await dbContext.Query<ComponentAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == new ComponentId(command.ComponentId)
                && candidate.ProjectId == projectId,
                cancellationToken);
        if (component is null)
        {
            return Error.NotFound("NOT_FOUND", "The component was not found.");
        }

        if (component.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The component changed. Reload it before deleting the resource group.",
                new Dictionary<string, object> { ["currentVersion"] = component.Version });
        }

        var containsResources = component.Resources.Any(resource => resource.ResourceGroupId == command.ResourceGroupId);
        var deleted = component.RemoveResourceGroup(command.ResourceGroupId, containsResources);
        return deleted.IsError
            ? deleted.Errors
            : new ModelCommandOutcome($"Resource group {command.ResourceGroupId:D} deleted.", []);
    }
}
