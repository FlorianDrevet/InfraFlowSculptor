using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

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

        if (command.DefaultIdentityResourceId is { } defaultIdentityResourceId)
        {
            if (!descriptor.Definition.TryGetProperty("identities", out var identities)
                || identities.GetProperty("userAssignedMax").GetInt32() == 0
                || !descriptor.Definition.TryGetProperty("applicationSettings", out var settings)
                || !string.Equals(settings.GetProperty("destination").GetString(), "environmentVariables", StringComparison.Ordinal))
            {
                return Error.Validation("VAL-LIA-IDENTITE", "Only an application that supports user-assigned identities can set a default identity.");
            }

            var identity = component.Resources.SingleOrDefault(resource => resource.Id == defaultIdentityResourceId);
            if (identity is null)
            {
                var otherComponents = await dbContext.Query<ComponentAggregate>()
                    .Where(candidate => candidate.ProjectId == project.Id && candidate.Id != component.Id)
                    .ToArrayAsync(cancellationToken);
                if (otherComponents.Any(candidate => candidate.Resources.Any(resource => resource.Id == defaultIdentityResourceId)))
                {
                    return Error.Validation("VAL-LIA-IDENTITE-PARTAGEE", "A user-assigned identity cannot be shared across deployment components.");
                }

                return Error.Validation("VAL-LIA-IDENTITE", "The default identity must be a user-assigned identity in the application's deployment component.");
            }

            if (!string.Equals(identity.Type, "UserAssignedIdentity", StringComparison.Ordinal))
            {
                return Error.Validation("VAL-LIA-IDENTITE", "The default identity must reference a user-assigned identity resource.");
            }
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
            command.Tags,
            command.DefaultIdentityResourceId);
        return resource.IsError
            ? resource.Errors
            : new ModelCommandOutcome($"Resource {resource.Value.LogicalName} created.", []);
    }
}
