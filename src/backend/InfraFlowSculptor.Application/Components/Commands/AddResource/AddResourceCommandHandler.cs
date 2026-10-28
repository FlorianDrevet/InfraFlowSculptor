using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.AddResource;

public sealed class AddResourceCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider)
    : ICommandHandler<AddResourceCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        AddResourceCommand command,
        CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(
            dbContext, currentOrganization, command.ProjectId, command.ComponentId, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var (project, component) = loaded.Value;
        var version = ResourceCommandSupport.CheckComponentVersion(component, command.ExpectedVersion);
        if (version.IsError)
        {
            return version.Errors;
        }

        var descriptor = ResourceCommandSupport.FindDescriptor(project, command.Type, catalogProvider);
        if (descriptor is null)
        {
            return Error.Validation("RESOURCE_TYPE_UNKNOWN", "The resource type is not present in the project's pinned catalogue.");
        }

        var parsed = ResourceCommandSupport.ParseProperties(command.Properties);
        if (parsed.IsError)
        {
            return parsed.Errors;
        }

        var properties = PropertyValueValidator.Validate(descriptor, parsed.Value);
        if (properties.IsError)
        {
            return properties.Errors;
        }

        var resource = component.AddResource(
            command.ResourceId,
            command.ResourceGroupId,
            descriptor.Type,
            command.LogicalName,
            command.Description,
            command.IsExisting,
            ResourceCommandSupport.GetTargetIds(component, project),
            parsed.Value,
            command.Tags);
        return resource.IsError
            ? resource.Errors
            : new ModelCommandOutcome($"Resource {resource.Value.LogicalName} created.", []);
    }
}
