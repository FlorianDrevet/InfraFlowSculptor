using System.Text.RegularExpressions;
using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Components.Commands.AddLink;
using InfraFlowSculptor.Application.Components.Commands.SetPresence;
using InfraFlowSculptor.Application.Components.Commands.UpdateAppSetting;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Application.Components.Validation;

public static partial class PilotFindingFixes
{
    public static IReadOnlyList<IFindingFix> Create(LinkKindRegistry linkKinds) =>
    [
        new RequiredLinkFindingFix(linkKinds),
        new ResourcePresenceFindingFix(),
        new SecretSuspectFindingFix()
    ];

    private sealed class RequiredLinkFindingFix(LinkKindRegistry linkKinds) : IFindingFix
    {
        public string Key => "VAL-LIA-OBLIGATOIRE";

        public ErrorOr<IModelCommand> CreateCommand(FindingFixContext context)
        {
            if (!Enum.TryParse<LinkKind>(context.Finding.DetailKey, out var expectedKind)
                || !Enum.IsDefined(expectedKind))
            {
                return Error.Validation("VALIDATION_FIX_TARGET_NOT_FOUND", "The required link kind was not found.");
            }

            var issue = context.Validation.Ordering.Wiring.Issues.SingleOrDefault(candidate =>
                candidate.Code == Key
                && candidate.ResourceId == context.Finding.Subject.Id
                && candidate.Kind == expectedKind);
            if (issue?.Kind is not { } kind)
            {
                return Error.NotFound("VALIDATION_FIX_TARGET_NOT_FOUND", "The resource or missing link was not found.");
            }

            var source = context.Snapshot.Components.SelectMany(component =>
                    component.Resources.Select(resource => (Component: component, Resource: resource)))
                .SingleOrDefault(candidate => candidate.Resource.Id == context.Finding.Subject.Id);
            if (source.Resource is null)
            {
                return Error.NotFound("VALIDATION_FIX_TARGET_NOT_FOUND", "The source resource was not found.");
            }

            if (!linkKinds.Supports(kind))
            {
                return Error.Validation("VALIDATION_FIX_UNAVAILABLE", "The required link cannot be fixed using the current catalog.");
            }

            var handler = linkKinds.Get(kind);
            var sourceDescriptor = context.Catalog.GetResourceType(source.Resource.Type);
            if (sourceDescriptor is null)
            {
                return Error.Validation("VALIDATION_FIX_UNAVAILABLE", "The resource type is not present in the pinned catalog.");
            }

            var targets = context.Snapshot.Components.SelectMany(component =>
                    component.Resources.Select(resource => (Component: component, Resource: resource)))
                .Where(candidate => candidate.Resource.Id != source.Resource.Id)
                .Where(candidate => context.Catalog.GetResourceType(candidate.Resource.Type) is { } targetDescriptor
                    && handler.Supports(sourceDescriptor, targetDescriptor))
                .ToArray();
            if (targets.Length != 1)
            {
                return Error.Conflict("VALIDATION_FIX_AMBIGUOUS", "The required link does not have exactly one compatible target.");
            }

            var target = targets[0];
            var targetDescriptor = context.Catalog.GetResourceType(target.Resource.Type)!;
            var identityRequirement = handler.GetIdentityRequirement(target.Resource.Properties);
            var sourceActsAsIdentity = handler.SourceActsAsIdentity(sourceDescriptor);
            var identity = ResolveIdentity(context.Snapshot, source.Component, sourceDescriptor, identityRequirement, sourceActsAsIdentity);
            if (identity.IsError)
            {
                return identity.Errors;
            }

            return new AddLinkCommand(
                context.ProjectId,
                source.Component.Id,
                Guid.NewGuid(),
                source.Resource.Id,
                target.Component.Id,
                target.Resource.Id,
                kind,
                identity.Value.Kind,
                identity.Value.ResourceId,
                [],
                LinkAccessScope.Resource,
                null,
                source.Component.Version);
        }

        private static ErrorOr<(LinkIdentityKind Kind, Guid? ResourceId)> ResolveIdentity(
            ModelSnapshot snapshot,
            ComponentSnapshot sourceComponent,
            CatalogResourceTypeDescriptor sourceDescriptor,
            LinkIdentityRequirement requirement,
            bool sourceActsAsIdentity)
        {
            if (requirement is LinkIdentityRequirement.None or LinkIdentityRequirement.Optional || sourceActsAsIdentity)
            {
                return (LinkIdentityKind.None, null);
            }

            if (requirement == LinkIdentityRequirement.Required
                && sourceDescriptor.Definition.GetProperty("identities").GetProperty("system").GetBoolean())
            {
                return (LinkIdentityKind.System, null);
            }

            var identities = snapshot.Components.Single(component => component.Id == sourceComponent.Id).Resources
                .Where(resource => string.Equals(resource.Type, "UserAssignedIdentity", StringComparison.Ordinal))
                .ToArray();
            if (identities.Length != 1)
            {
                return Error.Conflict("VALIDATION_FIX_IDENTITY_AMBIGUOUS", "The link requires an identity and the component does not have exactly one eligible identity.");
            }

            return (LinkIdentityKind.UserAssigned, identities[0].Id);
        }
    }

    private sealed class ResourcePresenceFindingFix : IFindingFix
    {
        public string Key => "VAL-RES-PRESENCE";

        public ErrorOr<IModelCommand> CreateCommand(FindingFixContext context)
        {
            var target = context.Snapshot.Components.SelectMany(component =>
                    component.Resources.Select(resource => (Component: component, Resource: resource)))
                .SingleOrDefault(candidate => candidate.Resource.Id == context.Finding.Subject.Id);
            if (target.Resource is null || context.Finding.EnvironmentId is not { } targetId)
            {
                return Error.NotFound("VALIDATION_FIX_TARGET_NOT_FOUND", "The resource target was not found.");
            }

            return new SetPresenceCommand(
                context.ProjectId,
                target.Component.Id,
                target.Resource.Id,
                targetId,
                true,
                target.Resource.Version);
        }
    }

    private sealed class SecretSuspectFindingFix : IFindingFix
    {
        public string Key => "VAL-PAR-SECRET-SUSPECT";

        public ErrorOr<IModelCommand> CreateCommand(FindingFixContext context)
        {
            var setting = context.Snapshot.AppSettings.SingleOrDefault(candidate => candidate.Id == context.Finding.Subject.Id);
            if (setting is null)
            {
                return Error.NotFound("VALIDATION_FIX_TARGET_NOT_FOUND", "The application setting was not found.");
            }

            var app = context.Snapshot.Components.SelectMany(component =>
                    component.Resources.Select(resource => (Component: component, Resource: resource)))
                .SingleOrDefault(candidate => candidate.Resource.Id == setting.ApplicationResourceId);
            var vaults = context.Snapshot.Components.SelectMany(component =>
                    component.Resources.Select(resource => (Component: component, Resource: resource)))
                .Where(candidate => string.Equals(candidate.Resource.Type, "KeyVault", StringComparison.Ordinal))
                .ToArray();
            if (app.Resource is null || vaults.Length != 1)
            {
                return Error.Conflict("VALIDATION_FIX_AMBIGUOUS", "A unique application and Key Vault are required to convert this value safely.");
            }

            var vault = vaults[0];
            if (!vault.Resource.IsExisting && vault.Component.Id == app.Component.Id)
            {
                return Error.Conflict("VALIDATION_FIX_ORDER_REQUIRED", "The Key Vault and its consumer must be deployed in separate components.");
            }

            var orderedComponents = context.Validation.OrderedComponents.ToList();
            var vaultIndex = orderedComponents.FindIndex(component => component.Id == vault.Component.Id);
            var applicationIndex = orderedComponents.FindIndex(component => component.Id == app.Component.Id);
            if (!vault.Resource.IsExisting
                && (vaultIndex < 0 || applicationIndex < 0 || vaultIndex >= applicationIndex))
            {
                return Error.Conflict("VALIDATION_FIX_ORDER_REQUIRED", "The Key Vault must be deployed before its consumer.");
            }

            var identityResourceId = app.Resource.DefaultIdentityResourceId;
            if (string.Equals(app.Resource.Type, "ContainerApp", StringComparison.Ordinal) && identityResourceId is null)
            {
                var identities = app.Component.Resources
                    .Where(resource => string.Equals(resource.Type, "UserAssignedIdentity", StringComparison.Ordinal))
                    .ToArray();
                if (identities.Length != 1)
                {
                    return Error.Conflict("VALIDATION_FIX_IDENTITY_AMBIGUOUS", "A unique user-assigned identity is required to read this Key Vault secret.");
                }

                identityResourceId = identities[0].Id;
            }

            var secretName = SecretNamePattern().Replace(setting.Name, "-").Trim('-');
            secretName = secretName[..Math.Min(secretName.Length, 127)];
            if (secretName.Length == 0)
            {
                return Error.Validation("VALIDATION_FIX_SECRET_NAME_INVALID", "The setting name cannot produce a valid Key Vault secret name.");
            }

            var pipelineVariable = $"{vault.Resource.LogicalName}_{secretName}"
                .Replace('-', '_')
                .ToUpperInvariant();
            if (!PipelineVariablePattern().IsMatch(pipelineVariable)
                || context.Snapshot.AppSettings.Any(candidate => candidate.Id != setting.Id
                    && candidate.SecretFeedKind == AppSettingSecretFeedKind.PipelineSecret
                    && string.Equals(candidate.PipelineVariableName, pipelineVariable, StringComparison.OrdinalIgnoreCase)))
            {
                return Error.Conflict("VALIDATION_FIX_PIPELINE_VARIABLE_CONFLICT", "The derived pipeline variable name is invalid or already used.");
            }

            if (context.Snapshot.AppSettings.Any(candidate => candidate.Id != setting.Id
                && candidate.KeyVaultResourceId == vault.Resource.Id
                && candidate.SecretFeedKind == AppSettingSecretFeedKind.PipelineSecret
                && string.Equals(candidate.SecretName, secretName, StringComparison.OrdinalIgnoreCase)))
            {
                return Error.Conflict("VALIDATION_FIX_SECRET_CONFLICT", "The selected Key Vault secret already has a pipeline writer.");
            }

            return new UpdateAppSettingCommand(
                context.ProjectId,
                setting.ComponentId,
                setting.ApplicationResourceId,
                setting.Id,
                new AppSettingDefinition(
                    setting.Name,
                    AppSettingSourceKind.KeyVaultSecret,
                    KeyVaultResourceId: vault.Resource.Id,
                    SecretName: secretName,
                    SecretFeedKind: AppSettingSecretFeedKind.PipelineSecret,
                    PipelineVariableName: pipelineVariable,
                    IdentityResourceId: identityResourceId),
                setting.Version);
        }
    }

    [GeneratedRegex("[^A-Za-z0-9-]", RegexOptions.CultureInvariant)]
    private static partial Regex SecretNamePattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex PipelineVariablePattern();
}
