using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Links;

public sealed class SecretReadLinkKindHandler() : LinkKindHandler(
    LinkKind.SecretRead,
    isImplicitOnly: true,
    createsDeploymentDependency: true,
    LinkIdentityRequirement.Required,
    new LinkFormDefinition([new LinkFormField("identity", "identity", true)]))
{
    public override string? RoleEquivalenceKey => "secretRead";

    protected override bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target) =>
        SupportsEnvironmentSettings(source) && target.Type == "KeyVault";

    private static bool SupportsEnvironmentSettings(CatalogResourceTypeDescriptor descriptor) =>
        descriptor.Definition.TryGetProperty("applicationSettings", out var settings)
        && string.Equals(settings.GetProperty("destination").GetString(), "environmentVariables", StringComparison.Ordinal);

    public override IReadOnlyList<string> GetRequiredRoles(
        LinkSnapshot link,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> targetProperties,
        CatalogResourceTypeDescriptor targetDescriptor) =>
        ["Key Vault Secrets User"];

    public override IReadOnlyList<LinkSnapshot> GetImplicitLinks(ModelSnapshot snapshot, CatalogVersion catalog)
    {
        var resources = snapshot.Components.SelectMany(component => component.Resources
            .Select(resource => (Component: component, Resource: resource)))
            .ToDictionary(entry => entry.Resource.Id);

        return snapshot.AppSettings
            .Where(setting => setting.SourceKind == InfraFlowSculptor.Domain.ComponentAggregate.AppSettingSourceKind.KeyVaultSecret
                && setting.KeyVaultResourceId is not null)
            .OrderBy(setting => setting.Id)
            .Where(setting => resources.ContainsKey(setting.ApplicationResourceId)
                && resources.ContainsKey(setting.KeyVaultResourceId!.Value))
            .Select(setting =>
            {
                var application = resources[setting.ApplicationResourceId];
                var vault = resources[setting.KeyVaultResourceId!.Value];
                return new LinkSnapshot(
                    LinkIdFactory.Create($"appSettingSecretRead:{setting.Id:N}", application.Resource.Id, vault.Resource.Id),
                    application.Component.Id,
                    application.Resource.Id,
                    vault.Component.Id,
                    vault.Resource.Id,
                    Key,
                    LinkOrigin.Implicit,
                    setting.IdentityResourceId is null && application.Resource.DefaultIdentityResourceId is null
                        ? LinkIdentityKind.System
                        : LinkIdentityKind.UserAssigned,
                    setting.IdentityResourceId ?? application.Resource.DefaultIdentityResourceId,
                    [],
                    LinkAccessScope.Resource,
                    null);
            })
            .ToArray();
    }
}
