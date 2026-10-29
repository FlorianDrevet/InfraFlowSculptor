using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;

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

    protected override bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target) =>
        source.Type == "ContainerApp" && target.Type == "ApplicationInsights";

    private static bool IsLocalAuthenticationDisabled(IReadOnlyDictionary<string, JsonElement> targetProperties) =>
        targetProperties.TryGetValue("disableLocalAuth", out var value)
        && value.ValueKind == JsonValueKind.True;
}
