using System.Text.Json;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Applications;
using InfraFlowSculptor.Engine.Tests.Builders;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Links;
using Xunit;

namespace InfraFlowSculptor.Engine.Tests.Applications;

public sealed class ApplicationPipelineTests
{
    private static readonly LinkKindRegistry LinkKinds = new(PilotLinkKindHandlers.Create());

    [Fact]
    public void SingleRegistryIsDerivedFromImagePullLinkWithoutPromotion()
    {
        var targetId = Guid.NewGuid();
        var application = CreateResource(Guid.NewGuid(), "ContainerApp", "api", targetId);
        var registry = CreateResource(Guid.NewGuid(), "ContainerRegistry", "main", targetId);
        var source = CreateSingleComponent(Guid.NewGuid(), "Orders", "orders", application, targetId);
        var registryComponent = CreateSingleComponent(Guid.NewGuid(), "Platform", "platform", registry, targetId);
        var snapshot = new ModelSnapshotBuilder()
            .WithComponent(source)
            .WithComponent(registryComponent)
            .Build() with
        {
            Links =
            [
                new LinkSnapshot(
                    Guid.NewGuid(), source.Id, application.Id, registryComponent.Id, registry.Id,
                    LinkKind.ImagePull, LinkOrigin.Explicit, LinkIdentityKind.None, null, [],
                    LinkAccessScope.Resource, null)
            ]
        };

        var result = BuildRegistryResolver.Resolve(snapshot, application.Id, LinkKinds);

        Assert.True(result.IsAvailable);
        Assert.Equal("crshopmainshared", result.RegistryName);
        Assert.Equal("platform / main", result.DisplayName);
        Assert.False(result.PromotionRequired);
        Assert.Empty(result.Options);
    }

    [Fact]
    public void UnrelatedNamingFindingDoesNotHideTheLinkedBuildRegistry()
    {
        var targetId = Guid.NewGuid();
        var application = CreateResource(Guid.NewGuid(), "ContainerApp", "api", targetId);
        var registry = CreateResource(Guid.NewGuid(), "ContainerRegistry", "main", targetId);
        var unrelatedKeyVault = CreateResource(Guid.NewGuid(), "KeyVault", "vault", targetId) with
        {
            ForcedNames = new Dictionary<Guid, string> { [targetId] = "not_valid" }
        };
        var source = CreateSingleComponent(Guid.NewGuid(), "Orders", "orders", application, targetId);
        var registryComponent = CreateSingleComponent(Guid.NewGuid(), "Platform", "platform", registry, targetId);
        var unrelatedComponent = CreateSingleComponent(Guid.NewGuid(), "Data", "data", unrelatedKeyVault, targetId);
        var snapshot = new ModelSnapshotBuilder()
            .WithComponent(source)
            .WithComponent(registryComponent)
            .WithComponent(unrelatedComponent)
            .Build() with
        {
            Links =
            [
                new LinkSnapshot(
                    Guid.NewGuid(), source.Id, application.Id, registryComponent.Id, registry.Id,
                    LinkKind.ImagePull, LinkOrigin.Explicit, LinkIdentityKind.None, null, [],
                    LinkAccessScope.Resource, null)
            ]
        };

        var result = BuildRegistryResolver.Resolve(snapshot, application.Id, LinkKinds);

        Assert.True(result.IsAvailable);
        Assert.Equal("crshopmainshared", result.RegistryName);
        Assert.Equal("platform / main", result.DisplayName);
    }

    [Fact]
    public void PerEnvironmentRegistrySelectsTheFirstEnvironmentAndRequiresPromotion()
    {
        var developmentId = Guid.NewGuid();
        var productionId = Guid.NewGuid();
        var application = CreateResource(Guid.NewGuid(), "ContainerApp", "api", developmentId);
        var registry = CreateResource(Guid.NewGuid(), "ContainerRegistry", "main", developmentId);
        var source = CreateSingleComponent(Guid.NewGuid(), "Orders", "orders", application, developmentId);
        var registryComponent = CreateSingleComponent(Guid.NewGuid(), "Platform", "platform", registry, developmentId) with
        {
            Mode = "PerEnvironment",
            SingleTarget = null,
            TargetEnvironmentIds = [developmentId, productionId],
            TargetsAllEnvironments = false
        };
        var snapshot = new ModelSnapshotBuilder()
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral") { Id = developmentId, Position = 0 })
            .WithEnvironment(new EnvironmentSnapshot("prd", "francecentral") { Id = productionId, Position = 1 })
            .WithComponent(source)
            .WithComponent(registryComponent)
            .Build() with
        {
            Links =
            [
                new LinkSnapshot(
                    Guid.NewGuid(), source.Id, application.Id, registryComponent.Id, registry.Id,
                    LinkKind.ImagePull, LinkOrigin.Explicit, LinkIdentityKind.None, null, [],
                    LinkAccessScope.Resource, null)
            ]
        };

        var result = BuildRegistryResolver.Resolve(snapshot, application.Id, LinkKinds);

        Assert.True(result.IsAvailable);
        Assert.True(result.PromotionRequired);
        Assert.Equal(developmentId, result.SelectedEnvironmentId);
        Assert.Equal("crshopmaindev", result.RegistryName);
        Assert.Equal(2, result.Options.Count);
    }

    [Fact]
    public void EnabledStepWithoutRequiredValueCreatesApplicationStepFinding()
    {
        using var document = JsonDocument.Parse("""
            {
              "name": "CustomStep",
              "labels": { "fr": "Étape personnalisée", "en": "Custom step" },
              "description": "Étape de test.",
              "defaults": { "enabled": true, "blocking": true, "position": "beforeBuild" },
              "parameters": [{ "name": "command", "kind": "string" }],
              "pipeline": { "provider": "AzurePipelines", "template": ".ifs/steps/custom.yml" }
            }
            """);
        var configured = new ApplicationStepDefinition("CustomStep", true, true, ApplicationStepPosition.BeforeBuild, []);

        var finding = Assert.Single(ApplicationStepValidator.Validate(
            [configured],
            [new InfraFlowSculptor.Catalog.CatalogPipelineStepDescriptor(document.RootElement)]));

        Assert.Equal("VAL-APP-ETAPE", finding.Code);
        Assert.Equal("command", finding.ParameterName);
    }

    [Fact]
    public void DirectStrategyAddsHealthCheckAfterDeployingTheImage()
    {
        var strategy = new DirectDeploymentStrategy();
        var definition = new ApplicationDefinition(
            "api", "src/api", "src/api/Dockerfile", "src/api", "shop/orders/api", "/healthz",
            DeploymentStrategyKind.Direct, null, []);

        var plan = strategy.CreatePlan(definition);

        Assert.Empty(plan.InfrastructurePrerequisites);
        Assert.Equal([DeploymentStageStepKind.DeployContainerImage, DeploymentStageStepKind.CheckHealth], plan.StageSteps);
    }

    private static ComponentSnapshot CreateSingleComponent(
        Guid componentId,
        string name,
        string code,
        ResourceSnapshot resource,
        Guid targetId)
    {
        var resourceGroup = new ResourceGroupSnapshot(
            resource.ResourceGroupId,
            "main",
            null,
            new Dictionary<string, string>(),
            new Dictionary<Guid, string>());
        return new ComponentSnapshot(
            componentId,
            name,
            code,
            "Single",
            [],
            new ComponentSingleTargetSnapshot(
                targetId, "shared", Guid.NewGuid(), "francecentral", "local", false, [], []),
            [resourceGroup],
            [resource]);
    }

    private static ResourceSnapshot CreateResource(Guid resourceGroupId, string type, string name, Guid targetId) => new(
        Guid.NewGuid(),
        resourceGroupId,
        type,
        name,
        string.Empty,
        false,
        new Dictionary<string, JsonElement>(),
        new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(),
        new Dictionary<Guid, bool> { [targetId] = true },
        new Dictionary<Guid, string>(),
        new Dictionary<Guid, string>(),
        new Dictionary<string, string>(),
        []);
}
