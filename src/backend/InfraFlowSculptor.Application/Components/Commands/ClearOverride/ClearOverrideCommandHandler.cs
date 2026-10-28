using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Catalog;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.ClearOverride;

public sealed class ClearOverrideCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider)
    : ICommandHandler<ClearOverrideCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        ClearOverrideCommand command,
        CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(
            dbContext, currentOrganization, command.ProjectId, command.ComponentId, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var (project, component) = loaded.Value;
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

        var descriptor = ResourceCommandSupport.FindDescriptor(project, resource.Type, catalogProvider);
        if (descriptor is null)
        {
            return Error.Validation("RESOURCE_TYPE_UNKNOWN", "The resource type is not present in the project's pinned catalogue.");
        }

        var valid = PropertyValueValidator.ValidateOverrideClear(
            descriptor,
            command.Property,
            PropertyValueValidator.ResolveEffectiveValues(
                descriptor,
                resource.GetProperties(),
                resource.GetProperties(command.TargetId)),
            PropertyValueValidator.ResolveEffectiveValues(descriptor, resource.GetProperties()),
            isPublished: component.FirstPublishedAt is not null);
        if (valid.IsError)
        {
            return valid.Errors;
        }

        var cleared = component.ClearResourceOverride(
            resource.Id, command.TargetId, command.Property);
        return cleared.IsError
            ? cleared.Errors
            : new ModelCommandOutcome($"Resource {resource.LogicalName} override cleared.", []);
    }
}
