using System.Text.Json;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Application;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Emitters.Bicep;
using InfraFlowSculptor.Emitters.AzureDevOps;
using InfraFlowSculptor.Emitters.InstallKit;
using InfraFlowSculptor.Engine.Plan;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Emitters.Tests;

public sealed class ReferenceOutputTests
{
    [Fact]
    public void BicepOutputCheckerIsAvailableFromTheLanguageRegistry()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddBicepEmitter();
        services.AddAzureDevOpsEmitters();
        services.AddInstallKitEmitter();
        using var provider = services.BuildServiceProvider();

        Assert.True(provider.GetRequiredService<OutputCheckerRegistry>().Supports(IacLanguage.Bicep));
        Assert.True(provider.GetRequiredService<PipelineEmitterRegistry>().Supports(CiPlatform.AzureDevOps));
        Assert.True(provider.GetRequiredService<InstallKitPlatformEmitterRegistry>().Supports(CiPlatform.AzureDevOps));
        Assert.True(provider.GetRequiredService<StepTranslatorRegistry>().Supports(CiPlatform.AzureDevOps));
    }

    [Fact]
    public void AzureDevOpsStepTranslatorUsesCatalogDefaultsAndConfiguredValues()
    {
        var catalog = CatalogLoader.Load().GetVersion("2026.10-pilot.6");
        Assert.NotNull(catalog);
        var descriptor = Assert.Single(catalog!.PipelineSteps, candidate => candidate.Name == "ImageScan");
        var step = new ApplicationStepPlan(
            "ImageScan",
            true,
            true,
            ApplicationStepPosition.AfterBuild,
            [new ApplicationStepParameterPlan("minimumSeverity", "HIGH")]);

        var translated = new AzureDevOpsStepTranslator().Translate(descriptor, step);

        Assert.Equal(".ifs/templates/steps/ImageScan.yml", translated.Template);
        Assert.Equal(ApplicationStepPosition.AfterBuild, translated.Position);
        Assert.Equal(["exceptions", "minimumSeverity", "tool"], translated.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(["", "HIGH", "Trivy"], translated.Parameters.Select(parameter => parameter.Value));
    }

    [Fact]
    public void UserAssignedIdentityIsNotGivenUnsupportedDiagnosticSettings()
    {
        var plan = LoadPilotPlan();
        var orders = Assert.Single(plan.Components, component => component.ComponentCode == "orders");

        Assert.DoesNotContain(orders.Targets.SelectMany(target => target.DiagnosticSettings),
            setting => setting.SourceResourceType == "UserAssignedIdentity");
    }

    [Fact]
    public void LogAnalyticsQuotaDefaultsToUnlimitedInGeneratedParameters()
    {
        var files = Emit(LoadPilotPlan());
        var parameters = Assert.Single(files, file => file.RelativePath == "core/infra/main.dev.bicepparam").Content;

        Assert.Contains("dailyQuotaGb: '-1'", parameters, StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalSqlDatabaseReferenceIncludesItsHostingServer()
    {
        var plan = LoadPilotPlan();
        var orders = Assert.Single(plan.Components, component => component.ComponentCode == "orders");
        var development = Assert.Single(orders.Targets, target => target.Target.Code == "dev");

        Assert.Contains(development.ExternalReferences, reference => reference.ResourceType == "SqlServer"
            && reference.LogicalName == "orders");
    }

    [Fact]
    public void PilotPlanEmitsBicepFilesThatMatchTheReference()
    {
        var plan = LoadPilotPlan();
        var catalog = CatalogLoader.Load().GetVersion(plan.Pins.Version);
        Assert.NotNull(catalog);
        var emitter = CreateEmitter();

        var actualFiles = emitter.Emit(plan, catalog!);
        var actual = actualFiles.ToDictionary(file => file.RelativePath, file => Normalize(file.Content, plan), StringComparer.Ordinal);
        var expected = ReadReferenceFiles();
        var differences = new List<string>();

        foreach (var path in expected.Keys.Union(actual.Keys, StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!expected.TryGetValue(path, out var expectedContent))
            {
                differences.Add($"{path}: emitted file is not present in the reference.");
                continue;
            }

            if (!actual.TryGetValue(path, out var actualContent))
            {
                differences.Add($"{path}: reference file was not emitted.");
                continue;
            }

            if (!string.Equals(expectedContent, actualContent, StringComparison.Ordinal))
            {
                differences.Add(DescribeDifference(path, expectedContent, actualContent));
            }
        }

        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    [Fact]
    public void PilotPlanEmitsTheCompleteReferenceTreeDeterministically()
    {
        var plan = LoadPilotPlan();
        var catalog = CatalogLoader.Load().GetVersion(plan.Pins.Version);
        Assert.NotNull(catalog);
        var first = Emit(plan);
        var second = Emit(plan);
        Assert.Equal(first, second);

        var actual = first.ToDictionary(file => file.RelativePath, file => Normalize(file.Content, plan), StringComparer.Ordinal);
        var expected = ReadCompleteReferenceFiles();
        var differences = new List<string>();

        foreach (var path in expected.Keys.Union(actual.Keys, StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!expected.TryGetValue(path, out var expectedContent))
            {
                differences.Add($"{path}: emitted file is not present in the reference.");
                continue;
            }

            if (!actual.TryGetValue(path, out var actualContent))
            {
                differences.Add($"{path}: reference file was not emitted.");
                continue;
            }

            if (!string.Equals(expectedContent, actualContent, StringComparison.Ordinal))
            {
                differences.Add(DescribeDifference(path, expectedContent, actualContent));
            }
        }

        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    [Fact]
    public void ReleaseAndDataAccessFilesMatchTheReference()
    {
        var plan = LoadPilotPlan();
        var catalog = CatalogLoader.Load().GetVersion(plan.Pins.Version);
        Assert.NotNull(catalog);
        var expected = ReadReferenceFiles()
            .Where(pair => IsReleaseOrDataAccessReference(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var actual = CreateEmitter().Emit(plan, catalog!)
            .Where(file => IsReleaseOrDataAccessReference(file.RelativePath))
            .ToDictionary(file => file.RelativePath, file => Normalize(file.Content, plan), StringComparer.Ordinal);

        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (path, expectedContent) in expected)
        {
            Assert.True(actual.TryGetValue(path, out var actualContent), $"{path}: reference file was not emitted.");
            Assert.True(string.Equals(expectedContent, actualContent, StringComparison.Ordinal),
                DescribeDifference(path, expectedContent, actualContent!));
        }
    }

    [Fact]
    public void DataAccessScriptsAreReusableAcrossDifferentDatabasesAndPrincipals()
    {
        var plan = UpdateTargetDataAccess(LoadPilotPlan(), "orders", "dev", accesses =>
        {
            var access = Assert.Single(accesses);
            return
            [
                access,
                access with
                {
                    Server = "sql-archive.database.windows.net",
                    Database = "archive",
                    PrincipalResourceId = "/subscriptions/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/resourceGroups/rg-shop-orders-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/api-archive"
                }
            ];
        });

        var files = Emit(plan);
        var applyScript = Assert.Single(files,
            file => file.RelativePath == "orders/infra/scripts/data-access.sql").Content;
        using var release = JsonDocument.Parse(Assert.Single(files,
            file => file.RelativePath == "orders/infra/release.dev.json").Content);
        var accesses = release.RootElement.GetProperty("dataAccess").EnumerateArray().ToArray();

        Assert.Contains("$(Roles)", applyScript, StringComparison.Ordinal);
        Assert.Equal(["sqldb-shop-orders-dev", "archive"],
            accesses.Select(access => access.GetProperty("database").GetString()));
    }

    [Fact]
    public void EmitIsDeterministic()
    {
        var plan = LoadPilotPlan();
        var catalog = CatalogLoader.Load().GetVersion(plan.Pins.Version);
        Assert.NotNull(catalog);
        var emitter = CreateEmitter();

        var first = emitter.Emit(plan, catalog!);
        var second = emitter.Emit(plan, catalog!);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ResourceGroupLogicalNamesWithHyphensAreMappedToValidBicepIdentifiers()
    {
        var plan = LoadPilotPlan();
        plan = UpdateTargetResourceGroups(plan, "orders", "dev", groups => groups.Append(
            new ResourceGroupPlan("observability-core", "rg-shop-observability-core-dev", "francecentral", true,
                new Dictionary<string, string>(StringComparer.Ordinal))).ToArray());
        plan = UpdateTargetResourceGroups(plan, "orders", "prd", groups => groups.Append(
            new ResourceGroupPlan("observability-core", "rg-shop-observability-core-prd", "francecentral", true,
                new Dictionary<string, string>(StringComparer.Ordinal))).ToArray());

        var files = Emit(plan);
        var main = Assert.Single(files, file => file.RelativePath == "orders/infra/main.bicep").Content;
        var parameters = Assert.Single(files, file => file.RelativePath == "orders/infra/main.dev.bicepparam").Content;
        var types = Assert.Single(files, file => file.RelativePath == "orders/infra/types.bicep").Content;

        Assert.Contains("resource rg_observability_core '", main, StringComparison.Ordinal);
        Assert.Contains("resourceGroups.observability_core.name", main, StringComparison.Ordinal);
        Assert.Contains("observability_core: {", parameters, StringComparison.Ordinal);
        Assert.Contains("observability_core: resourceGroupConfig", types, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingCatalogRequiredResourceGroupFailsBeforeEmission()
    {
        var plan = UpdateTargetResourceGroups(LoadPilotPlan(), "orders", "dev", groups =>
            groups.Where(group => group.LogicalName != "main").ToArray());

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("main", exception.Message, StringComparison.Ordinal);
        Assert.Contains("resource group", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("language", "Pulumi")]
    [InlineData("moduleSource", "ClientModules")]
    public void UnsupportedProjectGenerationOptionsFailInsteadOfBeingIgnored(string setting, string value)
    {
        var plan = LoadPilotPlan();
        var project = setting == "language"
            ? plan.Project with { InfrastructureLanguage = value }
            : plan.Project with { ModuleSource = value };
        plan = plan with { Project = project };

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains(value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServerWithMissingExposureFailsInsteadOfBecomingClosedSilently()
    {
        var plan = UpdateTarget(LoadPilotPlan(), "data", "dev", resources => resources.Select(resource =>
            resource.Type == "SqlServer" ? resource with { Exposure = null } : resource).ToArray());

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("exposure", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RestrictedKeyVaultEmitsPublicEndpointWithExplicitIpRules()
    {
        var plan = WithExposure(LoadPilotPlan(), "core", "prd", "KeyVault", "Restricted", "203.0.113.0/24");
        var files = Emit(plan);
        var main = Assert.Single(files, file => file.RelativePath == "core/infra/main.bicep").Content;
        var parameters = Assert.Single(files, file => file.RelativePath == "core/infra/main.prd.bicepparam").Content;

        Assert.Contains("publicNetworkAccess: kvMain.publicNetworkAccess", main, StringComparison.Ordinal);
        Assert.Contains("networkAcls:", main, StringComparison.Ordinal);
        Assert.Contains("defaultAction: kvMain.exposure == 'Restricted' ? 'Deny' : 'Allow'", main, StringComparison.Ordinal);
        Assert.Contains("value: ipRange", main, StringComparison.Ordinal);
        Assert.Contains("exposure: 'Restricted'", parameters, StringComparison.Ordinal);
        Assert.Contains("'203.0.113.0/24'", parameters, StringComparison.Ordinal);
    }

    [Fact]
    public void RestrictedKeyVaultRequiresAtLeastOneAllowedRange()
    {
        var plan = WithExposure(LoadPilotPlan(), "core", "prd", "KeyVault", "Restricted");

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("au moins une plage CIDR", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RestrictedSqlServerEmitsOnlyTheConfiguredFirewallRanges()
    {
        var plan = WithExposure(LoadPilotPlan(), "data", "prd", "SqlServer", "Restricted", "198.51.100.0/24");
        var files = Emit(plan);
        var main = Assert.Single(files, file => file.RelativePath == "data/infra/main.bicep").Content;
        var parameters = Assert.Single(files, file => file.RelativePath == "data/infra/main.prd.bicepparam").Content;

        Assert.Contains("publicNetworkAccess: sqlOrders.publicNetworkAccess", main, StringComparison.Ordinal);
        Assert.Contains("parseCidr(ipRange).network", main, StringComparison.Ordinal);
        Assert.Contains("parseCidr(ipRange).broadcast", main, StringComparison.Ordinal);
        Assert.Contains("exposure: 'Restricted'", parameters, StringComparison.Ordinal);
        Assert.Contains("'198.51.100.0/24'", parameters, StringComparison.Ordinal);
        Assert.Contains("'Enabled'", parameters, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServerRejectsTheAzureServicesSentinelAsARestrictedCidr()
    {
        var plan = WithExposure(LoadPilotPlan(), "data", "prd", "SqlServer", "Restricted", "0.0.0.0/32");

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("Azure Services", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RestrictedContainerRegistryEmitsRulesOnlyForPremiumSku()
    {
        var plan = WithExposure(LoadPilotPlan(), "platform", "shared", "ContainerRegistry", "Restricted", "192.0.2.0/24");
        plan = WithProperty(plan, "platform", "shared", "ContainerRegistry", "sku", "Premium");
        var files = Emit(plan);
        var main = Assert.Single(files, file => file.RelativePath == "platform/infra/main.bicep").Content;
        var parameters = Assert.Single(files, file => file.RelativePath == "platform/infra/main.shared.bicepparam").Content;

        Assert.Contains("networkRuleSetIpRules:", main, StringComparison.Ordinal);
        Assert.Contains("networkRuleSetDefaultAction: acrMain.networkRuleSetDefaultAction", main, StringComparison.Ordinal);
        Assert.Contains("action: 'Allow'", main, StringComparison.Ordinal);
        Assert.Contains("value: ipRange", main, StringComparison.Ordinal);
        Assert.Contains("exposure: 'Restricted'", parameters, StringComparison.Ordinal);
        Assert.Contains("networkRuleSetDefaultAction: 'Deny'", parameters, StringComparison.Ordinal);
        Assert.Contains("'192.0.2.0/24'", parameters, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicContainerRegistryAllowsUnrestrictedPublicAccess()
    {
        var files = Emit(LoadPilotPlan());
        var parameters = Assert.Single(files, file => file.RelativePath == "platform/infra/main.shared.bicepparam").Content;

        Assert.Contains("networkRuleSetDefaultAction: 'Allow'", parameters, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainerRegistryExposureActionIsResolvedPerTarget()
    {
        var plan = LoadPilotPlan();
        var platform = Assert.Single(plan.Components, component => component.ComponentCode == "platform");
        var shared = Assert.Single(platform.Targets);
        var publicTarget = WithRegistryExposure(shared, "dev", "Public", [], "Standard");
        var restrictedTarget = WithRegistryExposure(shared, "prd", "Restricted", ["192.0.2.0/24"], "Premium");
        var updatedPlatform = platform with
        {
            Mode = ComponentDeploymentMode.PerEnvironment,
            Targets = [publicTarget, restrictedTarget]
        };
        plan = plan with
        {
            Components = plan.Components.Select(component => component.ComponentCode == "platform"
                ? updatedPlatform
                : component).ToArray()
        };

        var files = Emit(plan);
        var main = Assert.Single(files, file => file.RelativePath == "platform/infra/main.bicep").Content;
        var publicParameters = Assert.Single(files, file => file.RelativePath == "platform/infra/main.dev.bicepparam").Content;
        var restrictedParameters = Assert.Single(files, file => file.RelativePath == "platform/infra/main.prd.bicepparam").Content;

        Assert.Contains("networkRuleSetIpRules:", main, StringComparison.Ordinal);
        Assert.Contains("networkRuleSetDefaultAction: 'Allow'", publicParameters, StringComparison.Ordinal);
        Assert.Contains("exposure: 'Public'", publicParameters, StringComparison.Ordinal);
        Assert.Contains("allowedIpRanges: []", publicParameters, StringComparison.Ordinal);
        Assert.Contains("networkRuleSetDefaultAction: 'Deny'", restrictedParameters, StringComparison.Ordinal);
        Assert.Contains("exposure: 'Restricted'", restrictedParameters, StringComparison.Ordinal);
        Assert.Contains("'192.0.2.0/24'", restrictedParameters, StringComparison.Ordinal);
        Assert.Equal(files.Select(file => (file.RelativePath, file.Content)),
            Emit(plan).Select(file => (file.RelativePath, file.Content)));
    }

    [Fact]
    public void RestrictedContainerRegistryRejectsNonPremiumSku()
    {
        var plan = WithExposure(LoadPilotPlan(), "platform", "shared", "ContainerRegistry", "Restricted", "192.0.2.0/24");

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("Premium", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PrivateSqlExposureIsRejectedUntilPrivateEndpointsAreGenerated()
    {
        var plan = WithExposure(LoadPilotPlan(), "data", "prd", "SqlServer", "Private");

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("points de terminaison privés SQL", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServerRejectsSqlAuthenticationUntilSecretDeliveryIsSupported()
    {
        var plan = WithProperty(LoadPilotPlan(), "data", "dev", "SqlServer", "authentication", "EntraEtSql");

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("EntraEtSql", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServerWithoutAnEmbeddedDatabaseFailsInsteadOfEmittingAnEmptyDatabaseName()
    {
        var plan = UpdateTarget(LoadPilotPlan(), "data", "dev", resources =>
            resources.Where(resource => resource.Type != "SqlDatabase").ToArray());

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("requires an embedded 'SqlDatabase' child", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedContainerAppTransportFailsInsteadOfBeingIgnored()
    {
        var plan = WithProperty(LoadPilotPlan(), "orders", "dev", "ContainerApp", "transport", "http2");

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("transport", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainerAppWithoutIngressFailsInsteadOfCreatingAnInternalIngress()
    {
        var plan = WithProperty(LoadPilotPlan(), "orders", "dev", "ContainerApp", "ingress", "None");

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("ingress", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("None", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainerAppWithoutRequiredExternalRegistryFailsBeforeEmission()
    {
        var plan = UpdateTargetExternalReferences(LoadPilotPlan(), "orders", "dev", references =>
            references.Where(reference => reference.ResourceType != "ContainerRegistry").ToArray());

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("ContainerApp", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ContainerRegistry", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainerAppWithoutRequiredLocalIdentityFailsBeforeEmission()
    {
        var plan = UpdateTarget(LoadPilotPlan(), "orders", "dev", resources =>
            resources.Where(resource => resource.Type != "UserAssignedIdentity").ToArray());

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("ContainerApp", exception.Message, StringComparison.Ordinal);
        Assert.Contains("UserAssignedIdentity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainerAppWithoutRequiredPaymentsSecretFailsBeforeEmission()
    {
        var plan = UpdateTargetAppSettings(LoadPilotPlan(), "orders", "dev", settings =>
            settings.Where(setting => setting.Name != "Payments__ApiKey").ToArray());

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("Payments__ApiKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainerAppRejectsApplicationSettingsThatItsTemplateDoesNotEmit()
    {
        var plan = UpdateTargetAppSettings(LoadPilotPlan(), "orders", "dev", settings => settings.Append(
            new AppSettingPlan("CUSTOM__SETTING", "environmentVariable", "literal", "value", null, null,
                null, null, null, null, null, false, false)).ToArray());

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("CUSTOM__SETTING", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainerAppRejectsPaymentsSecretWithoutAKeyVaultSecretName()
    {
        var plan = UpdateTargetAppSettings(LoadPilotPlan(), "orders", "dev", settings => settings.Select(setting =>
            setting.Name == "Payments__ApiKey" ? setting with { SecretName = string.Empty } : setting).ToArray());

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("Payments__ApiKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingLocalResourcesFailInsteadOfBeingOmittedFromBicep()
    {
        var plan = UpdateTarget(LoadPilotPlan(), "core", "dev", resources => resources.Select(resource =>
            resource.Type == "LogAnalyticsWorkspace" ? resource with { IsExisting = true } : resource).ToArray());

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("existing", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LogAnalyticsWorkspace", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleLocalResourcesOfOneTypeFailInsteadOfDroppingAnInstance()
    {
        var plan = UpdateTarget(LoadPilotPlan(), "orders", "dev", resources =>
        {
            var app = Assert.Single(resources, resource => resource.Type == "ContainerApp");
            return resources.Append(app with { LogicalName = "apiReplica", Name = "ca-shop-api-replica-dev" }).ToArray();
        });

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("ContainerApp", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleEmbeddedDatabasesFailInsteadOfKeepingOnlyTheFirst()
    {
        var plan = UpdateTarget(LoadPilotPlan(), "data", "dev", resources =>
        {
            var database = Assert.Single(resources, resource => resource.Type == "SqlDatabase");
            return resources.Append(database with
            {
                LogicalName = "ordersArchive",
                Name = "sqldb-orders-archive-dev",
                ResourceId = "/subscriptions/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/resourceGroups/rg-shop-data-main-dev/providers/Microsoft.Sql/servers/sql-orders-dev/databases/ordersArchive"
            }).ToArray();
        });

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("SqlDatabase", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateGeneratedHelperPathsFailInsteadOfReturningCollidingFiles()
    {
        var plan = LoadPilotPlan();
        var vault = Assert.Single(plan.Components, component => component.ComponentCode == "core")
            .Targets[0].Resources.Single(resource => resource.Type == "KeyVault");
        plan = UpdateTarget(plan, "orders", "dev", resources => resources.Append(vault).ToArray());

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("key-vault-role-assignment.bicep", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleExternalResourcesSharingOneBicepParameterPathFailInsteadOfOverwriting()
    {
        var plan = LoadPilotPlan();
        var orders = Assert.Single(plan.Components, component => component.ComponentCode == "orders");
        var development = Assert.Single(orders.Targets, target => target.Target.Code == "dev");
        var keyVault = Assert.Single(development.ExternalReferences, reference => reference.ResourceType == "KeyVault");
        var duplicate = keyVault with
        {
            LogicalName = "alternate",
            Name = "kv-shop-alternate-dev",
            ResourceId = "/subscriptions/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/resourceGroups/rg-shop-core-main-dev/providers/Microsoft.KeyVault/vaults/kv-shop-alternate-dev"
        };
        plan = WithExternalReference(plan, "orders", "dev", duplicate);

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("coreKeyVault", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticDestinationThatDiffersFromCatalogRouteIsRejected()
    {
        var plan = WithDiagnosticDestination(LoadPilotPlan(), "core", "dev", "alternate");

        var exception = Assert.Throws<InvalidOperationException>(() => Emit(plan));

        Assert.Contains("diagnostic destination", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BicepStringLiteralEscapesQuotesBackslashesInterpolationAndLineBreaks()
    {
        var writer = new BicepWriter();
        BicepValueWriter.WriteProperty(writer, "123dynamic-tag", "O'Hara\\${environment}\nsecond line");

        Assert.Equal("""'123dynamic-tag': 'O\'Hara\\\${environment}\nsecond line'""" + "\n", writer.ToString());
    }

    [Fact]
    public void UnresolvedCatalogPlaceholderIsRejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => BicepCatalogMetadata.EnsureNoUnresolvedPlaceholders("var role = '{{role:Missing Role}}'"));

        Assert.Contains("{{role:Missing Role}}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryCatalogPropertyIsMappedOrExplicitlyRejectedByBicepMetadata()
    {
        var catalog = CatalogLoader.Load().GetVersion("2026.10-pilot.6");
        Assert.NotNull(catalog);

        foreach (var descriptor in catalog!.ResourceTypes)
        {
            var bicep = BicepCatalogMetadata.GetBicep(descriptor);
            var supported = new HashSet<string>(StringComparer.Ordinal);
            if (bicep.TryGetProperty("config", out var config)
                && config.TryGetProperty("fields", out var fields))
            {
                foreach (var field in fields.EnumerateArray())
                {
                    var source = field.GetProperty("source");
                    var kind = BicepCatalogMetadata.ReadString(source, "kind");
                    if (kind is "property" or "exposure"
                        && BicepCatalogMetadata.ReadString(source, "field") is { } propertyName)
                    {
                        supported.Add(propertyName);
                    }
                }
            }

            foreach (var parent in catalog.ResourceTypes)
            {
                var parentBicep = BicepCatalogMetadata.GetBicep(parent);
                if (!parentBicep.TryGetProperty("embeddedChildren", out var children)
                    || !children.EnumerateArray().Any(child => string.Equals(
                        BicepCatalogMetadata.ReadString(child, "type"), descriptor.Type, StringComparison.Ordinal))
                    || !parentBicep.TryGetProperty("config", out var parentConfig)
                    || !parentConfig.TryGetProperty("fields", out var parentFields))
                {
                    continue;
                }

                foreach (var field in parentFields.EnumerateArray())
                {
                    var source = field.GetProperty("source");
                    if (string.Equals(BicepCatalogMetadata.ReadString(source, "kind"), "child", StringComparison.Ordinal)
                        && BicepCatalogMetadata.ReadString(source, "field") is { } propertyName)
                    {
                        supported.Add(propertyName);
                    }
                }
            }

            var unsupported = bicep.TryGetProperty("unsupportedValues", out var unsupportedValues)
                ? unsupportedValues.EnumerateArray()
                    .Select(item => BicepCatalogMetadata.ReadString(item, "property")!)
                    .ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in descriptor.Definition.GetProperty("properties").EnumerateArray())
            {
                var name = BicepCatalogMetadata.ReadString(property, "name")!;
                Assert.True(supported.Contains(name) || unsupported.Contains(name),
                    $"{descriptor.Type}.{name} must be emitted from a catalog source or rejected unless it matches its default.");
            }
        }
    }

    [Fact]
    public void ExposurePlanSortsAndDeduplicatesAllowedRanges()
    {
        var plan = WithExposure(LoadPilotPlan(), "core", "prd", "KeyVault", "Restricted",
            "203.0.113.128/25", "203.0.113.0/25", "203.0.113.128/25");
        var vault = Assert.Single(plan.Components, component => component.ComponentCode == "core")
            .Targets.Single(target => target.Target.Code == "prd")
            .Resources.Single(resource => resource.Type == "KeyVault");

        Assert.Equal(["203.0.113.0/25", "203.0.113.128/25"], vault.Exposure!.AllowedIpRanges);
    }

    private static BicepInfrastructureEmitter CreateEmitter() =>
        new(new ModuleSourceStrategyRegistry([new PublicAvmModuleSourceStrategy()]));

    private static DeploymentPlan LoadPilotPlan()
        => PilotReferenceModel.Create(CatalogLoader.Load().GetVersion("2026.10-pilot.6")
            ?? throw new InvalidOperationException("The reference catalog version is unavailable."));

    private static GeneratedFile[] Emit(DeploymentPlan plan)
    {
        var catalog = CatalogLoader.Load().GetVersion(plan.Pins.Version)
            ?? throw new InvalidOperationException("The reference catalog version is unavailable.");
        return CreateEmitter().Emit(plan, catalog)
            .Concat(new AzureDevOpsPipelineEmitter().Emit(plan, catalog))
            .Concat(InstallKitEmitter.Emit(plan))
            .Concat(new AzureDevOpsInstallKitPlatformEmitter().Emit(plan, catalog))
            .Append(ReadmeEmitter.Emit(plan))
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static DeploymentPlan WithExposure(
        DeploymentPlan plan,
        string componentCode,
        string targetCode,
        string resourceType,
        string mode,
        params string[] ranges) => plan with
        {
            Components = plan.Components.Select(component =>
                string.Equals(component.ComponentCode, componentCode, StringComparison.Ordinal)
                    ? component with
                    {
                        Targets = component.Targets.Select(target =>
                            string.Equals(target.Target.Code, targetCode, StringComparison.Ordinal)
                                ? target with
                                {
                                    Resources = target.Resources.Select(resource =>
                                        string.Equals(resource.Type, resourceType, StringComparison.Ordinal)
                                            ? resource with { Exposure = new ExposurePlan(mode, ranges) }
                                            : resource).ToArray()
                                }
                                : target).ToArray()
                    }
                    : component).ToArray()
        };

    private static TargetPlan WithRegistryExposure(
        TargetPlan source,
        string targetCode,
        string mode,
        IReadOnlyList<string> allowedIpRanges,
        string sku) => source with
        {
            Target = source.Target with { Code = targetCode },
            Resources = source.Resources.Select(resource => resource.Type == "ContainerRegistry"
                ? resource with
                {
                    Exposure = new ExposurePlan(mode, allowedIpRanges),
                    Properties = resource.Properties.Select(property => property.Name == "sku"
                        ? property with { Value = JsonSerializer.SerializeToElement(sku) }
                        : property).ToArray()
                }
                : resource).ToArray()
        };

    private static DeploymentPlan WithProperty(
        DeploymentPlan plan,
        string componentCode,
        string targetCode,
        string resourceType,
        string propertyName,
        object value) => plan with
        {
            Components = plan.Components.Select(component =>
                string.Equals(component.ComponentCode, componentCode, StringComparison.Ordinal)
                    ? component with
                    {
                        Targets = component.Targets.Select(target =>
                            string.Equals(target.Target.Code, targetCode, StringComparison.Ordinal)
                                ? target with
                                {
                                    Resources = target.Resources.Select(resource =>
                                        string.Equals(resource.Type, resourceType, StringComparison.Ordinal)
                                            ? resource with
                                            {
                                                Properties = resource.Properties.Select(property =>
                                                    string.Equals(property.Name, propertyName, StringComparison.Ordinal)
                                                        ? property with { Value = JsonSerializer.SerializeToElement(value) }
                                                        : property).ToArray()
                                            }
                                            : resource).ToArray()
                                }
                                : target).ToArray()
                    }
                    : component).ToArray()
        };

    private static DeploymentPlan UpdateTarget(
        DeploymentPlan plan,
        string componentCode,
        string targetCode,
        Func<IReadOnlyList<ResourcePlan>, IReadOnlyList<ResourcePlan>> updateResources) => plan with
        {
            Components = plan.Components.Select(component =>
                string.Equals(component.ComponentCode, componentCode, StringComparison.Ordinal)
                    ? component with
                    {
                        Targets = component.Targets.Select(target =>
                            string.Equals(target.Target.Code, targetCode, StringComparison.Ordinal)
                                ? target with { Resources = updateResources(target.Resources) }
                                : target).ToArray()
                    }
                    : component).ToArray()
        };

    private static DeploymentPlan UpdateTargetResourceGroups(
        DeploymentPlan plan,
        string componentCode,
        string targetCode,
        Func<IReadOnlyList<ResourceGroupPlan>, IReadOnlyList<ResourceGroupPlan>> updateResourceGroups) => plan with
        {
            Components = plan.Components.Select(component =>
                string.Equals(component.ComponentCode, componentCode, StringComparison.Ordinal)
                    ? component with
                    {
                        Targets = component.Targets.Select(target =>
                            string.Equals(target.Target.Code, targetCode, StringComparison.Ordinal)
                                ? target with { ResourceGroups = updateResourceGroups(target.ResourceGroups) }
                                : target).ToArray()
                    }
                    : component).ToArray()
        };

    private static DeploymentPlan WithExternalReference(
        DeploymentPlan plan,
        string componentCode,
        string targetCode,
        ExternalReference reference) => plan with
        {
            Components = plan.Components.Select(component =>
                string.Equals(component.ComponentCode, componentCode, StringComparison.Ordinal)
                    ? component with
                    {
                        Targets = component.Targets.Select(target =>
                            string.Equals(target.Target.Code, targetCode, StringComparison.Ordinal)
                                ? target with { ExternalReferences = target.ExternalReferences.Append(reference).ToArray() }
                                : target).ToArray()
                    }
                    : component).ToArray()
        };

    private static DeploymentPlan UpdateTargetExternalReferences(
        DeploymentPlan plan,
        string componentCode,
        string targetCode,
        Func<IReadOnlyList<ExternalReference>, IReadOnlyList<ExternalReference>> updateReferences) => plan with
        {
            Components = plan.Components.Select(component =>
                string.Equals(component.ComponentCode, componentCode, StringComparison.Ordinal)
                    ? component with
                    {
                        Targets = component.Targets.Select(target =>
                            string.Equals(target.Target.Code, targetCode, StringComparison.Ordinal)
                                ? target with { ExternalReferences = updateReferences(target.ExternalReferences) }
                                : target).ToArray()
                    }
                    : component).ToArray()
        };

    private static DeploymentPlan UpdateTargetAppSettings(
        DeploymentPlan plan,
        string componentCode,
        string targetCode,
        Func<IReadOnlyList<AppSettingPlan>, IReadOnlyList<AppSettingPlan>> updateSettings) => plan with
        {
            Components = plan.Components.Select(component =>
                string.Equals(component.ComponentCode, componentCode, StringComparison.Ordinal)
                    ? component with
                    {
                        Targets = component.Targets.Select(target =>
                            string.Equals(target.Target.Code, targetCode, StringComparison.Ordinal)
                                ? target with { AppSettings = updateSettings(target.AppSettings) }
                                : target).ToArray()
                    }
                    : component).ToArray()
        };

    private static DeploymentPlan UpdateTargetDataAccess(
        DeploymentPlan plan,
        string componentCode,
        string targetCode,
        Func<IReadOnlyList<DataAccessPlan>, IReadOnlyList<DataAccessPlan>> updateDataAccess) => plan with
        {
            Components = plan.Components.Select(component =>
                string.Equals(component.ComponentCode, componentCode, StringComparison.Ordinal)
                    ? component with
                    {
                        Targets = component.Targets.Select(target =>
                            string.Equals(target.Target.Code, targetCode, StringComparison.Ordinal)
                                ? WithUpdatedDataAccess(target, updateDataAccess(target.DataAccess))
                                : target).ToArray()
                    }
                    : component).ToArray()
        };

    private static TargetPlan WithUpdatedDataAccess(
        TargetPlan target,
        IReadOnlyList<DataAccessPlan> dataAccess) => target with
        {
            DataAccess = dataAccess,
            Release = target.Release with
            {
                DataAccess = dataAccess.Select(access => new ReleaseDataAccessPlan(
                    access.Server,
                    access.Database,
                    new ReleaseDataAccessPrincipalPlan(access.PrincipalResourceId, access.PrincipalKind),
                    access.Level,
                    access.Exposure)).ToArray()
            }
        };

    private static DeploymentPlan WithDiagnosticDestination(
        DeploymentPlan plan,
        string componentCode,
        string targetCode,
        string logicalName) => plan with
        {
            Components = plan.Components.Select(component =>
                string.Equals(component.ComponentCode, componentCode, StringComparison.Ordinal)
                    ? component with
                    {
                        Targets = component.Targets.Select(target =>
                            string.Equals(target.Target.Code, targetCode, StringComparison.Ordinal)
                                ? target with
                                {
                                    DiagnosticSettings = target.DiagnosticSettings.Select(setting => setting with
                                    {
                                        Destination = setting.Destination with { LogicalName = logicalName }
                                    }).ToArray()
                                }
                                : target).ToArray()
                    }
                    : component).ToArray()
        };

    private static Dictionary<string, string> ReadReferenceFiles()
    {
        var root = FindRepositoryRoot();
        var referenceRoot = Path.Combine(root, "reference", "pilot", "bicep-azdo");
        return Directory.GetFiles(referenceRoot, "*", SearchOption.AllDirectories)
            .Where(path => IsBicepReference(referenceRoot, path))
            .ToDictionary(
                path => Path.GetRelativePath(referenceRoot, path).Replace(Path.DirectorySeparatorChar, '/'),
                File.ReadAllText,
                StringComparer.Ordinal);
    }

    private static Dictionary<string, string> ReadCompleteReferenceFiles()
    {
        var root = FindRepositoryRoot();
        var referenceRoot = Path.Combine(root, "reference", "pilot", "bicep-azdo");
        return Directory.GetFiles(referenceRoot, "*", SearchOption.AllDirectories)
            .Where(path => IsBicepReference(referenceRoot, path) || IsPipelineInstallOrReadmeReference(referenceRoot, path))
            .ToDictionary(
                path => Path.GetRelativePath(referenceRoot, path).Replace(Path.DirectorySeparatorChar, '/'),
                File.ReadAllText,
                StringComparer.Ordinal);
    }

    private static bool IsPipelineInstallOrReadmeReference(string referenceRoot, string path)
    {
        var relative = Path.GetRelativePath(referenceRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/');
        return relative == "README.ifs.md"
            || relative.Contains("/pipelines/", StringComparison.Ordinal)
                && path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
            || relative is ".ifs/install/azure-setup.ps1" or ".ifs/install/install.pipeline.yml" or ".ifs/install/SETUP.md"
            || relative is ".ifs/templates/infra-pr.yml" or ".ifs/templates/infra-ci.yml"
                or ".ifs/templates/infra-release.yml" or ".ifs/templates/infra-target-stages.yml"
                or ".ifs/templates/app-pr.yml" or ".ifs/templates/app-ci.yml"
                or ".ifs/templates/app-release.yml" or ".ifs/templates/app-target-stage.yml"
                or ".ifs/templates/scripts/IfsRelease.psm1"
                or ".ifs/templates/scripts/Invoke-IfsInfraPreview.ps1"
                or ".ifs/templates/scripts/Invoke-IfsInfraDeploy.ps1"
                or ".ifs/templates/scripts/Invoke-IfsAppDeploy.ps1";
    }

    private static bool IsBicepReference(string referenceRoot, string path)
    {
        var relative = Path.GetRelativePath(referenceRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/');
        var fileName = Path.GetFileName(path);
        return relative.Contains("/infra/", StringComparison.Ordinal)
            && (path.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".bicepparam", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("release.", StringComparison.Ordinal)
                    && fileName.EndsWith(".json", StringComparison.Ordinal)
                || relative.Contains("/scripts/", StringComparison.Ordinal)
                    && path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsReleaseOrDataAccessReference(string relativePath)
    {
        var path = relativePath.Replace('\\', '/');
        var fileName = Path.GetFileName(path);
        return fileName.StartsWith("release.", StringComparison.Ordinal)
                && fileName.EndsWith(".json", StringComparison.Ordinal)
            || path.Contains("/scripts/", StringComparison.Ordinal)
                && path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NEXT.md")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root containing NEXT.md could not be found.");
    }

    private static string Normalize(string content, DeploymentPlan plan)
    {
        var placeholders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["dev"] = "<A>",
            ["prd"] = "<B>",
            ["shared"] = "<C>"
        };
        foreach (var target in plan.Components.SelectMany(component => component.Targets).Select(target => target.Target)
            .GroupBy(target => target.Code, StringComparer.Ordinal).Select(group => group.First()))
        {
            if (placeholders.TryGetValue(target.Code, out var placeholder))
            {
                content = content.Replace(target.SubscriptionId.ToString("D"), placeholder, StringComparison.OrdinalIgnoreCase);
            }
        }

        content = content.Replace(
            "00000000-0000-4000-8000-000000000001",
            "<SQL_ADMIN_GROUP_OBJECT_ID_DEV>",
            StringComparison.OrdinalIgnoreCase);
        content = content.Replace(
            "00000000-0000-4000-8000-000000000002",
            "<SQL_ADMIN_GROUP_OBJECT_ID_PRD>",
            StringComparison.OrdinalIgnoreCase);

        return content;
    }

    private static string DescribeDifference(string path, string expected, string actual)
    {
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        var line = 0;
        while (line < expectedLines.Length && line < actualLines.Length
            && string.Equals(expectedLines[line], actualLines[line], StringComparison.Ordinal))
        {
            line++;
        }

        var expectedLine = line < expectedLines.Length ? expectedLines[line] : "<fin du fichier>";
        var actualLine = line < actualLines.Length ? actualLines[line] : "<fin du fichier>";
        return $"{path}:{line + 1}: expected: {expectedLine}{Environment.NewLine}{path}:{line + 1}: actual:   {actualLine}";
    }
}
