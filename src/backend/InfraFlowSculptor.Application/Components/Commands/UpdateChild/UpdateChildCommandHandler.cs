using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Catalog;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.UpdateChild;

public sealed class UpdateChildCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider)
    : ICommandHandler<UpdateChildCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        UpdateChildCommand command,
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

        var child = resource.Children.SingleOrDefault(candidate => candidate.Id == command.ChildId);
        if (child is null)
        {
            return Error.NotFound("RESOURCE_CHILD_NOT_FOUND", "The child was not found.");
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

        var validName = ResourceCommandSupport.ValidateChildName(descriptor, child.Type, command.Name);
        if (validName.IsError)
        {
            return validName.Errors;
        }

        var validProperties = PropertyValueValidator.ValidateChildProperties(
            descriptor,
            child.Type,
            parsed.Value,
            isPublished: component.FirstPublishedAt is not null,
            currentValues: child.GetProperties());
        if (validProperties.IsError)
        {
            return validProperties.Errors;
        }

        var updated = component.UpdateResourceChild(resource.Id, child.Id, command.Name, parsed.Value);
        return updated.IsError
            ? updated.Errors
            : new ModelCommandOutcome($"Child {child.Name} updated.", []);
    }
}
