using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Catalog;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.AddChild;

public sealed class AddChildCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider)
    : ICommandHandler<AddChildCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        AddChildCommand command,
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

        if (resource.IsExisting)
        {
            return Error.Validation("RESOURCE_EXISTING_CHILDREN_INVALID", "An existing resource cannot define children.");
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

        var validName = ResourceCommandSupport.ValidateChildName(descriptor, command.Type, command.Name);
        if (validName.IsError)
        {
            return validName.Errors;
        }

        var validProperties = PropertyValueValidator.ValidateChildProperties(descriptor, command.Type, parsed.Value);
        if (validProperties.IsError)
        {
            return validProperties.Errors;
        }

        var added = component.AddResourceChild(
            resource.Id, command.ChildId, command.Type, command.Name, parsed.Value);
        return added.IsError
            ? added.Errors
            : new ModelCommandOutcome($"Child {added.Value.Name} created.", []);
    }
}
