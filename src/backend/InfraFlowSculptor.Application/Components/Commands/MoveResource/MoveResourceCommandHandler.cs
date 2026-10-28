using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.MoveResource;

public sealed class MoveResourceCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization)
    : ICommandHandler<MoveResourceCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        MoveResourceCommand command,
        CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(
            dbContext, currentOrganization, command.ProjectId, command.ComponentId, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var component = loaded.Value.Component;
        var resource = component.Resources.SingleOrDefault(candidate => candidate.Id == command.ResourceId);
        if (resource is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        var version = ResourceCommandSupport.CheckResourceVersion(resource, command.ExpectedVersion);
        if (version.IsError)
        {
            return version.Errors;
        }

        var moved = component.MoveResource(resource.Id, command.ResourceGroupId);
        return moved.IsError
            ? moved.Errors
            : new ModelCommandOutcome($"Resource {resource.LogicalName} moved.", []);
    }
}
