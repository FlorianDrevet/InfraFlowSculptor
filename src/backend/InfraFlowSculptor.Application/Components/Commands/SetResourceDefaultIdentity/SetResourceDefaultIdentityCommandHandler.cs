using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.SetResourceDefaultIdentity;

public sealed class SetResourceDefaultIdentityCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider)
    : ICommandHandler<SetResourceDefaultIdentityCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        SetResourceDefaultIdentityCommand command,
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

        if (command.IdentityResourceId is { } identityResourceId)
        {
            if (await dbContext.Query<AppSettingAggregate>().AnyAsync(setting =>
                setting.ProjectId == new ProjectId(command.ProjectId)
                && setting.ApplicationResourceId == resource.Id
                && setting.NameKey == ApplicationSettingNameRules.AzureClientId,
                cancellationToken))
            {
                return Error.Validation("VAL-PAR-IMPLICITE-CONFLIT",
                    $"The application already declares {ApplicationSettingNameRules.AzureClientId} explicitly, so it cannot add the implicit default-identity setting.");
            }

            var descriptor = ResourceCommandSupport.FindDescriptor(project, resource.Type, catalogProvider);
            if (descriptor is null
                || !descriptor.Definition.TryGetProperty("identities", out var identities)
                || identities.GetProperty("userAssignedMax").GetInt32() == 0
                || !descriptor.Definition.TryGetProperty("applicationSettings", out var settings)
                || !string.Equals(settings.GetProperty("destination").GetString(),
                    "environmentVariables", StringComparison.Ordinal))
            {
                return Error.Validation("VAL-LIA-IDENTITE", "Only an application that supports user-assigned identities can set a default identity.");
            }

            var identity = component.Resources.SingleOrDefault(candidate => candidate.Id == identityResourceId);
            if (identity is null)
            {
                var otherComponents = await dbContext.Query<ComponentAggregate>()
                    .Where(candidate => candidate.ProjectId == project.Id && candidate.Id != component.Id)
                    .ToArrayAsync(cancellationToken);
                if (otherComponents.Any(candidate => candidate.Resources.Any(item => item.Id == identityResourceId)))
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
        else if (string.Equals(resource.Type, "ContainerApp", StringComparison.Ordinal)
            && await dbContext.Query<AppSettingAggregate>().AnyAsync(setting =>
                setting.ProjectId == new ProjectId(command.ProjectId)
                && setting.ApplicationResourceId == resource.Id
                && setting.SourceKind == AppSettingSourceKind.KeyVaultSecret
                && setting.IdentityResourceId == null,
                cancellationToken))
        {
            return Error.Validation("RG-LIA-08-IDENTITY-REQUIRED",
                "A Container App with Key Vault settings needs a default or per-setting user-assigned identity.");
        }

        var updated = component.SetResourceDefaultIdentity(resource.Id, command.IdentityResourceId);
        return updated.IsError
            ? updated.Errors
            : new ModelCommandOutcome($"Resource {resource.LogicalName} default identity updated.", []);
    }
}
