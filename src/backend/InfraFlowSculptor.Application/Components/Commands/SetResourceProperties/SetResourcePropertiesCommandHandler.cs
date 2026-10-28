using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Catalog;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.SetResourceProperties;

public sealed class SetResourcePropertiesCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider)
    : ICommandHandler<SetResourcePropertiesCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        SetResourcePropertiesCommand command,
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

        var parsed = ResourceCommandSupport.ParseProperties(command.Properties);
        if (parsed.IsError)
        {
            return parsed.Errors;
        }

        var valid = PropertyValueValidator.Validate(
            descriptor,
            parsed.Value,
            isPublished: component.FirstPublishedAt is not null,
            currentValues: PropertyValueValidator.ResolveEffectiveValues(descriptor, resource.GetProperties()));
        if (valid.IsError)
        {
            return valid.Errors;
        }

        var updated = component.SetResourceProperties(resource.Id, parsed.Value);
        return updated.IsError
            ? updated.Errors
            : new ModelCommandOutcome($"Resource {resource.LogicalName} properties updated.", []);
    }
}
