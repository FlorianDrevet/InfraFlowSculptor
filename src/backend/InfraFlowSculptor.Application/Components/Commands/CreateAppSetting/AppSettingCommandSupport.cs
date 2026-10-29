using System.Text.Json;
using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.CreateAppSetting;

internal static class AppSettingCommandSupport
{
    public static async ValueTask<ErrorOr<ModelCommandOutcome>> CreateAsync(
        IIfsDbContext dbContext,
        ICurrentOrganization currentOrganization,
        ICatalogProvider catalogProvider,
        ModelSnapshotBuilder snapshotBuilder,
        WiringEngine wiringEngine,
        Guid projectId,
        Guid componentId,
        Guid applicationResourceId,
        Guid appSettingId,
        AppSettingDefinition definition,
        CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(dbContext, currentOrganization, projectId, componentId, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var (project, component) = loaded.Value;
        var application = component.Resources.SingleOrDefault(resource => resource.Id == applicationResourceId);
        if (application is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The application resource was not found.");
        }

        var catalog = catalogProvider.GetVersion(project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var applicationDescriptor = catalog.GetResourceType(application.Type);
        if (applicationDescriptor is null
            || !string.Equals(applicationDescriptor.Definition.GetProperty("applicationSettings").GetProperty("destination").GetString(),
                "environmentVariables", StringComparison.Ordinal))
        {
            return Error.Validation("VAL-PAR-DESTINATION", "The application type does not support environment variable settings in the pinned catalog.");
        }

        var projectComponents = await dbContext.Query<ComponentAggregate>()
            .Where(candidate => candidate.ProjectId == new ProjectId(projectId))
            .ToArrayAsync(cancellationToken);
        var resources = projectComponents.SelectMany(candidate => candidate.Resources
            .Select(resource => (Component: candidate, Resource: resource))).ToArray();
        definition = ApplyPipelineVariableDefault(definition, resources);
        var references = ValidateReferences(project, component, application, definition, resources, catalog);
        if (references.IsError)
        {
            return references.Errors;
        }

        var existingSettings = await dbContext.Query<AppSettingAggregate>()
            .Where(setting => setting.ProjectId == new ProjectId(projectId))
            .ToArrayAsync(cancellationToken);
        var explicitNameConflict = await ValidateImplicitNameConflictAsync(
            snapshotBuilder, wiringEngine, catalog, new ProjectId(projectId), applicationResourceId, definition, cancellationToken);
        if (explicitNameConflict.IsError)
        {
            return explicitNameConflict.Errors;
        }

        var created = AppSettingAggregate.Create(
            appSettingId,
            new ProjectId(projectId),
            project.OrganizationId,
            componentId,
            applicationResourceId,
            definition,
            existingSettings);
        if (created.IsError)
        {
            return created.Errors;
        }

        dbContext.Query<AppSettingAggregate>().Add(created.Value);
        IReadOnlyList<string> findings = created.Value.SourceKind == AppSettingSourceKind.Literal
            && SecretValueDetector.HasSecretLikeName(created.Value.Name)
                ? [$"VAL-PAR-SECRET-SUSPECT: The literal setting name resembles a secret; convert it to a pipeline secret when appropriate."]
                : [];
        return new ModelCommandOutcome($"Application setting {created.Value.Name} created.", findings);
    }

    public static async Task<ErrorOr<Success>> ValidateImplicitNameConflictAsync(
        ModelSnapshotBuilder snapshotBuilder,
        WiringEngine wiringEngine,
        CatalogVersion catalog,
        ProjectId projectId,
        Guid applicationResourceId,
        AppSettingDefinition definition,
        CancellationToken cancellationToken)
    {
        var normalizedName = definition.Name?.Trim() ?? string.Empty;
        var snapshot = await snapshotBuilder.BuildAsync(projectId.Value, cancellationToken);
        if (snapshot.IsError)
        {
            return snapshot.Errors;
        }

        var implicitNames = wiringEngine.Evaluate(snapshot.Value, catalog).ImplicitApplicationSettings
            .Where(setting => setting.ApplicationResourceId == applicationResourceId)
            .Select(setting => setting.Name);
        var conflict = implicitNames.FirstOrDefault(name =>
            ApplicationSettingNameRules.ConflictsWithImplicit(normalizedName, name));
        return conflict is null
            ? Result.Success
            : Error.Validation("VAL-PAR-IMPLICITE-CONFLIT", $"The setting '{normalizedName}' is generated implicitly by a resource link and cannot be declared explicitly.");
    }

    public static AppSettingDefinition ApplyPipelineVariableDefault(
        AppSettingDefinition definition,
        IReadOnlyCollection<(ComponentAggregate Component, Resource Resource)> resources)
    {
        if (definition.SourceKind != AppSettingSourceKind.KeyVaultSecret
            || definition.SecretFeedKind != AppSettingSecretFeedKind.PipelineSecret
            || !string.IsNullOrWhiteSpace(definition.PipelineVariableName)
            || definition.KeyVaultResourceId is not { } vaultId)
        {
            return definition;
        }

        var vault = resources.SingleOrDefault(entry => entry.Resource.Id == vaultId).Resource;
        if (vault is null || string.IsNullOrWhiteSpace(definition.SecretName))
        {
            return definition;
        }

        var variableName = $"{vault.LogicalName}_{definition.SecretName}"
            .Replace('-', '_')
            .ToUpperInvariant();
        return definition with { PipelineVariableName = variableName };
    }

    public static ErrorOr<Success> ValidateReferences(
        ProjectAggregate project,
        ComponentAggregate component,
        Resource application,
        AppSettingDefinition definition,
        IReadOnlyCollection<(ComponentAggregate Component, Resource Resource)> resources,
        CatalogVersion catalog)
    {
        if (definition.SourceKind == AppSettingSourceKind.Literal)
        {
            var targetIds = ResourceCommandSupport.GetTargetIds(component, project);
            if (definition.LiteralOverrides?.Keys.Any(targetId => !targetIds.Contains(targetId)) == true)
            {
                return Error.Validation("VAL-PAR-OVERRIDE-TARGET", "A literal override must target an environment or deployment target of this application.");
            }
        }

        if (definition.SourceKind == AppSettingSourceKind.ResourceOutput)
        {
            var source = resources.SingleOrDefault(entry => entry.Resource.Id == definition.SourceResourceId);
            if (source.Resource is null)
            {
                return Error.Validation("VAL-PAR-SOURCE-RESOURCE", "The output source must be a resource in the same project.");
            }

            var descriptor = catalog.GetResourceType(source.Resource.Type);
            var output = descriptor?.Definition.GetProperty("outputs").EnumerateArray()
                .FirstOrDefault(candidate => string.Equals(candidate.GetProperty("name").GetString(), definition.OutputName, StringComparison.Ordinal));
            if (output is null || output.Value.ValueKind == JsonValueKind.Undefined)
            {
                return Error.Validation("VAL-PAR-OUTPUT-UNKNOWN", "The selected output is not declared by the pinned resource descriptor.");
            }

            if (output.Value.GetProperty("sensitive").GetBoolean())
            {
                return Error.Validation("VAL-PAR-04", "A sensitive resource output cannot be used as an environment variable. Store it as a Key Vault secret instead.");
            }
        }

        if (definition.SourceKind == AppSettingSourceKind.KeyVaultSecret)
        {
            var vault = resources.SingleOrDefault(entry => entry.Resource.Id == definition.KeyVaultResourceId);
            if (vault.Resource is null || !string.Equals(vault.Resource.Type, "KeyVault", StringComparison.Ordinal))
            {
                return Error.Validation("VAL-PAR-KEYVAULT", "The secret source must reference a Key Vault resource in the same project.");
            }

            if (definition.SecretFeedKind == AppSettingSecretFeedKind.PipelineSecret
                && !vault.Resource.IsExisting
                && vault.Component.Id == component.Id)
            {
                return Error.Validation("VAL-SEC-ORDRE", "A Key Vault that receives a pipeline secret must be deployed in a component before its consuming application.");
            }

            var selectedIdentityResourceId = definition.IdentityResourceId ?? application.DefaultIdentityResourceId;
            if (string.Equals(application.Type, "ContainerApp", StringComparison.Ordinal)
                && selectedIdentityResourceId is null)
            {
                return Error.Validation("RG-LIA-08-IDENTITY-REQUIRED", "A Container App must use a user-assigned identity to resolve a Key Vault secret during creation.");
            }

            if (selectedIdentityResourceId is { } identityId)
            {
                var identity = resources.SingleOrDefault(entry => entry.Resource.Id == identityId);
                if (identity.Resource is null)
                {
                    return Error.Validation("VAL-LIA-IDENTITE", "The selected user-assigned identity must exist in the project.");
                }

                if (identity.Component.Id != component.Id)
                {
                    return Error.Validation("VAL-LIA-IDENTITE-PARTAGEE", "A user-assigned identity cannot be shared across deployment components.");
                }

                if (!string.Equals(identity.Resource.Type, "UserAssignedIdentity", StringComparison.Ordinal))
                {
                    return Error.Validation("VAL-LIA-IDENTITE", "The selected identity must be a user-assigned identity resource.");
                }
            }
        }

        return Result.Success;
    }
}
