using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.SetPresence;

public sealed class SetPresenceCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization)
    : ICommandHandler<SetPresenceCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        SetPresenceCommand command,
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

        var updated = component.SetResourcePresence(resource.Id, command.TargetId, command.IsPresent);
        return updated.IsError
            ? updated.Errors
            : new ModelCommandOutcome($"Resource {resource.LogicalName} presence updated.", []);
    }
}
