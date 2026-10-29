using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Links;

public sealed class TelemetryLinkKindHandler() : LinkKindHandler(
    LinkKind.Telemetry,
    isImplicitOnly: false,
    createsDeploymentDependency: true,
    LinkIdentityRequirement.Optional,
    new LinkFormDefinition([new LinkFormField("identity", "identity", false)]))
{
    public override int? MaximumFor(CatalogResourceTypeDescriptor source) => 1;

    public override LinkIdentityRequirement GetIdentityRequirement(
        IReadOnlyDictionary<string, JsonElement> targetProperties) => IsLocalAuthenticationDisabled(targetProperties)
            ? LinkIdentityRequirement.Required
            : LinkIdentityRequirement.Optional;

    public override IReadOnlyList<string> GetRequiredRoles(
        LinkSnapshot link,
        IReadOnlyDictionary<string, JsonElement> targetProperties,
        CatalogResourceTypeDescriptor targetDescriptor) => IsLocalAuthenticationDisabled(targetProperties)
            ? ["Monitoring Metrics Publisher"]
            : [];

    public override IReadOnlyList<ImplicitApplicationSetting> GetImplicitApplicationSettings(
        ModelSnapshot snapshot,
        CatalogVersion catalog,
        IReadOnlyList<LinkSnapshot> links)
    {
        var resources = snapshot.Components.SelectMany(component => component.Resources
            .Select(resource => (Component: component, Resource: resource)))
            .ToDictionary(entry => entry.Resource.Id);
        var settings = new List<ImplicitApplicationSetting>();

        foreach (var link in links.OrderBy(value => value.Id))
        {
            if (!resources.TryGetValue(link.SourceResourceId, out var application)
                || !resources.TryGetValue(link.TargetResourceId, out var telemetry))
            {
                continue;
            }

            var applicationDescriptor = catalog.GetResourceType(application.Resource.Type);
            var telemetryDescriptor = catalog.GetResourceType(telemetry.Resource.Type);
            if (applicationDescriptor is null || telemetryDescriptor is null
                || !Supports(applicationDescriptor, telemetryDescriptor)
                || FindOutput(telemetryDescriptor, "connectionString") is not { } connectionString)
            {
                continue;
            }

            settings.Add(new ImplicitApplicationSetting(
                LinkIdFactory.Create("implicitAppSetting:Telemetry:APPLICATIONINSIGHTS_CONNECTION_STRING",
                    application.Resource.Id, telemetry.Resource.Id),
                application.Resource.Id,
                "APPLICATIONINSIGHTS_CONNECTION_STRING",
                telemetry.Resource.Id,
                "connectionString",
                null,
                connectionString.GetProperty("sensitive").GetBoolean(),
                "Telemetry"));
        }

        return settings;
    }

    protected override bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target) =>
        SupportsEnvironmentSettings(source) && FindOutput(target, "connectionString") is not null;

    private static bool SupportsEnvironmentSettings(CatalogResourceTypeDescriptor descriptor) =>
        descriptor.Definition.TryGetProperty("applicationSettings", out var settings)
        && string.Equals(settings.GetProperty("destination").GetString(), "environmentVariables", StringComparison.Ordinal);

    private static JsonElement? FindOutput(CatalogResourceTypeDescriptor descriptor, string name) =>
        descriptor.Definition.GetProperty("outputs").EnumerateArray()
            .FirstOrDefault(candidate => string.Equals(candidate.GetProperty("name").GetString(), name, StringComparison.Ordinal)) is { ValueKind: not JsonValueKind.Undefined } output
            ? output
            : null;

    private static bool IsLocalAuthenticationDisabled(IReadOnlyDictionary<string, JsonElement> targetProperties) =>
        targetProperties.TryGetValue("disableLocalAuth", out var value)
        && value.ValueKind == JsonValueKind.True;
}
