using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Tests.Links;

public sealed class ValueDependencyDeploymentTests
{
    private static readonly CatalogVersion Catalog = CatalogLoader.Load().GetVersion("2026.10-pilot")!;

    [Fact]
    public void ComputableOutputFromNameDoesNotCreateDeploymentDependency()
    {
        var consumerComponentId = Guid.NewGuid();
        var providerComponentId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var vaultId = Guid.NewGuid();
        var setting = new AppSettingSnapshot(
            Guid.NewGuid(),
            consumerComponentId,
            applicationId,
            "PAYMENTS__VAULT_URI",
            AppSettingSourceKind.ResourceOutput,
            null,
            new Dictionary<Guid, string>(),
            vaultId,
            "name",
            null,
            null,
            null,
            null,
            null);
        var snapshot = new ModelSnapshot(
            new ProjectSnapshot("Project", "project", string.Empty),
            [],
            new NamingConventionSnapshot("Compact", "{name}", "rg-{name}"),
            new Dictionary<string, string>(),
            [
                Component(consumerComponentId, "consumer", Resource(applicationId, "ContainerApp")),
                Component(providerComponentId, "provider", Resource(vaultId, "KeyVault"))
            ])
        {
            AppSettings = [setting]
        };

        var result = CreateEngine().Evaluate(snapshot, Catalog);

        Assert.Contains(result.Links, link => link.Link.Kind == LinkKind.ValueDependency);
        Assert.DoesNotContain(result.Dependencies, dependency => dependency.LinkKind == LinkKind.ValueDependency);
    }

    private static WiringEngine CreateEngine() => new(
        new LinkKindRegistry(PilotLinkKindHandlers.Create()),
        new DataAccessProviderRegistry([new AzureSqlDataAccessProvider()]));

    private static ComponentSnapshot Component(Guid id, string code, ResourceSnapshot resource) => new(
        id,
        code,
        code,
        "PerEnvironment",
        [],
        null,
        [new ResourceGroupSnapshot(resource.ResourceGroupId, "group", null, new Dictionary<string, string>(), new Dictionary<Guid, string>())],
        [resource]);

    private static ResourceSnapshot Resource(Guid id, string type) => new(
        id,
        Guid.NewGuid(),
        type,
        type.ToLowerInvariant(),
        string.Empty,
        false,
        new Dictionary<string, JsonElement>(),
        new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(),
        new Dictionary<Guid, bool>(),
        new Dictionary<Guid, string>(),
        new Dictionary<Guid, string>(),
        new Dictionary<string, string>(),
        []);
}
