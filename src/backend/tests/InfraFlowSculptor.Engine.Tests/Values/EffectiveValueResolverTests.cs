using System.Text.Json;
using AwesomeAssertions;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Values;

namespace InfraFlowSculptor.Engine.Tests.Values;

public sealed class EffectiveValueResolverTests
{
    private static readonly Guid DevId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly ICatalogProvider Catalog = CatalogLoader.Load(typeof(CatalogLoader).Assembly);
    private static readonly JsonElement KeyVaultDescriptor = Catalog
        .GetVersion("2026.10-pilot")!
        .GetResourceType("KeyVault")!
        .Definition;

    [Fact]
    public void UsesCatalogDefaultWhenNoOverrideOrResourceValueExists()
    {
        var values = EffectiveValueResolver.Resolve(Resource(), DevId, KeyVaultDescriptor);

        var purgeProtection = values.Single(value => value.Name == "purgeProtection");
        purgeProtection.Value!.Value.GetBoolean().Should().BeTrue();
        purgeProtection.Provenance.Should().Be(EffectiveValueProvenance.CatalogDefault);

        var retention = values.Single(value => value.Name == "softDeleteRetentionInDays");
        retention.Value!.Value.GetInt32().Should().Be(90);
        retention.Provenance.Should().Be(EffectiveValueProvenance.CatalogDefault);
    }

    [Fact]
    public void IncludesPropertiesWithRequiresConditionOnlyWhenTheEffectiveDependencyMatches()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"sku","default":"Basic","overridable":true},{"name":"zoneRedundancy","default":false,"condition":"requires:sku=Premium"}]}""");
        var resource = Resource(
            properties: Values(("sku", "Basic"), ("zoneRedundancy", false)),
            overrides: new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>
            {
                [DevId] = Values(("sku", "Premium"))
            });

        var values = EffectiveValueResolver.Resolve(resource, DevId, descriptor.RootElement);

        values.Select(value => value.Name).Should().Contain("zoneRedundancy");
        values.Single(value => value.Name == "zoneRedundancy").Value!.Value.GetBoolean().Should().BeFalse();
        values.Single(value => value.Name == "zoneRedundancy").Provenance.Should().Be(EffectiveValueProvenance.Resource);

        var basicValues = EffectiveValueResolver.Resolve(
            Resource(properties: Values(("sku", "Basic"), ("zoneRedundancy", true))),
            DevId,
            descriptor.RootElement);

        basicValues.Select(value => value.Name).Should().NotContain("zoneRedundancy");
    }

    [Fact]
    public void ExcludesRequiresConditionWhenDependencyIsAbsent()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"virtualNetworkConfiguration","kind":"object"},{"name":"zoneRedundant","default":false,"condition":"requires:virtualNetworkConfiguration"}]}""");

        var values = EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement);

        values.Select(value => value.Name).Should().NotContain("zoneRedundant");
    }

    [Fact]
    public void ExcludesPropertyWhoseConditionDependsOnAnotherInapplicableProperty()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"sku","default":"Basic"},{"name":"advancedMode","default":true,"condition":"requires:sku=Premium"},{"name":"backupWindow","default":"02:00","condition":"requires:advancedMode"}]}""");

        var values = EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement);

        values.Select(value => value.Name).Should().NotContain("advancedMode");
        values.Select(value => value.Name).Should().NotContain("backupWindow");
    }

    [Fact]
    public void ComparesNumericConditionValuesByNumericValue()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"cpu","default":1.0},{"name":"advancedMode","default":true,"condition":"requires:cpu=1"}]}""");

        var values = EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement);

        values.Select(value => value.Name).Should().Contain("advancedMode");
    }

    [Fact]
    public void RequiresWithoutExpectedValueTreatsFalseAndZeroAsPresentValues()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"enabled","default":false},{"name":"minReplicas","default":0},{"name":"falseDependent","default":true,"condition":"requires:enabled"},{"name":"zeroDependent","default":true,"condition":"requires:minReplicas"}]}""");

        var values = EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement);

        values.Select(value => value.Name).Should().Contain("falseDependent");
        values.Select(value => value.Name).Should().Contain("zeroDependent");
    }

    [Fact]
    public void RequiresWithoutExpectedValueTreatsAnExplicitlyEmptyStringAsPresent()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"optionalText","default":"","kind":"string"},{"name":"textDependent","default":true,"condition":"requires:optionalText"},{"name":"emptyTextDependent","default":true,"condition":"requires:optionalText="}]}""");

        var values = EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement);

        values.Select(value => value.Name).Should().Contain("textDependent");
        values.Select(value => value.Name).Should().Contain("emptyTextDependent");
    }

    [Fact]
    public void DoesNotMatchAnEmptyExpectedValueAgainstAnObject()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"payload","default":{}},{"name":"payloadDependent","default":true,"condition":"requires:payload="}]}""");

        var values = EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement);

        values.Select(value => value.Name).Should().NotContain("payloadDependent");
    }

    [Fact]
    public void RejectsAnEmptyConditionDependency()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"sku","default":"Basic"},{"name":"dependent","default":true,"condition":"requires:"}]}""");

        Assert.Throws<ArgumentException>(() => EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement));
    }

    [Fact]
    public void UnknownConditionDependencyIsReturnedAsUnresolved()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"sku","default":"Basic"},{"name":"dependent","default":false,"condition":"requires:unknown=Premium"}]}""");

        var result = EffectiveValueResolver.ResolveDetailed(Resource(), DevId, descriptor.RootElement);

        Assert.Equal("sku", Assert.Single(result.Values).Name);
        var issue = Assert.Single(result.UnresolvedConditions);
        Assert.Equal("dependent", issue.PropertyName);
        Assert.Equal("unknown", issue.DependencyName);
        Assert.False(issue.HasNonDefaultValue);
    }

    [Fact]
    public void UnknownConditionDependencyRecordsConfiguredNonDefaultValue()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"dependent","default":false,"condition":"requires:unknown=Premium"}]}""");
        var resource = Resource(properties: Values(("dependent", true)));

        var result = EffectiveValueResolver.ResolveDetailed(resource, DevId, descriptor.RootElement);

        var issue = Assert.Single(result.UnresolvedConditions);
        Assert.True(issue.HasNonDefaultValue);
    }

    [Fact]
    public void RejectsCyclicPropertyConditions()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"first","default":true,"condition":"requires:second"},{"name":"second","default":true,"condition":"requires:first"}]}""");

        Assert.Throws<ArgumentException>(() => EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement));
    }

    [Fact]
    public void AppliesRequiredWhenAsAnApplicabilityCondition()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"sku","default":"Basic","overridable":true},{"name":"capacity","default":100,"condition":"requiredWhen:sku=CapacityReservation"}]}""");

        var basicValues = EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement);
        var reservationValues = EffectiveValueResolver.Resolve(
            Resource(overrides: new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>
            {
                [DevId] = Values(("sku", "CapacityReservation"))
            }),
            DevId,
            descriptor.RootElement);

        basicValues.Select(value => value.Name).Should().NotContain("capacity");
        reservationValues.Select(value => value.Name).Should().Contain("capacity");
    }

    [Fact]
    public void ComparesLargeNumericConditionValuesExactly()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"large","default":1e30},{"name":"matching","default":true,"condition":"requires:large=1000000000000000000000000000000"},{"name":"different","default":true,"condition":"requires:large=1000000000000000000000000000001"}]}""");

        var values = EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement);

        values.Select(value => value.Name).Should().Contain("matching");
        values.Select(value => value.Name).Should().NotContain("different");
    }

    [Fact]
    public void ComparesVeryLargeNumericConditionValuesExactly()
    {
        var integer = $"1{new string('0', 10_000)}";
        using var descriptor = JsonDocument.Parse(
            $$"""{"properties":[{"name":"large","default":{{integer}}},{"name":"matching","default":true,"condition":"requires:large=1e10000"},{"name":"different","default":true,"condition":"requires:large=1e10001"}]}""");

        var values = EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement);

        values.Select(value => value.Name).Should().Contain("matching");
        values.Select(value => value.Name).Should().NotContain("different");
    }

    [Fact]
    public void DoesNotWrapAnExponentWhenComparingExtremelyLargeNumbers()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"large","default":10e9223372036854775807},{"name":"different","default":true,"condition":"requires:large=1e-9223372036854775808"}]}""");

        var values = EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement);

        values.Select(value => value.Name).Should().NotContain("different");
    }

    [Fact]
    public void RejectsMalformedPropertyDefinitionsWithAnArgumentException()
    {
        var malformedDescriptors = new[]
        {
            """{"properties":[{"default":1}]}""",
            """{"properties":[{"name":1}]}""",
            """{"properties":[null]}""",
            """{}"""
        };

        foreach (var malformedDescriptor in malformedDescriptors)
        {
            using var descriptor = JsonDocument.Parse(malformedDescriptor);
            Assert.Throws<ArgumentException>(() => EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement));
        }
    }

    [Fact]
    public void RejectsAnUnsupportedConditionPrefixInsteadOfExposingTheProperty()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"sku","default":"Basic"},{"name":"zoneRedundancy","default":false,"condition":"requres:sku=Premium"}]}""");

        Assert.Throws<ArgumentException>(() => EffectiveValueResolver.Resolve(Resource(), DevId, descriptor.RootElement));
    }

    [Fact]
    public void IgnoresStoredOverrideForNonOverridableProperty()
    {
        var resource = Resource(
            properties: Values(("softDeleteRetentionInDays", 60)),
            overrides: new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>
            {
                [DevId] = Values(("softDeleteRetentionInDays", 30))
            });

        var value = EffectiveValueResolver.Resolve(resource, DevId, KeyVaultDescriptor)
            .Single(candidate => candidate.Name == "softDeleteRetentionInDays");

        value.Value!.Value.GetInt32().Should().Be(60);
        value.Provenance.Should().Be(EffectiveValueProvenance.Resource);
    }

    [Fact]
    public void HidesCapacityReservationLevelUnlessTheSkuRequiresIt()
    {
        var descriptor = Descriptor("LogAnalyticsWorkspace");

        var perGbValues = EffectiveValueResolver.Resolve(
            Resource(type: "LogAnalyticsWorkspace"),
            DevId,
            descriptor);
        perGbValues.Select(value => value.Name).Should().NotContain("capacityReservationLevel");

        var capacityReservationValues = EffectiveValueResolver.Resolve(
            Resource(
                type: "LogAnalyticsWorkspace",
                overrides: new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>
                {
                    [DevId] = Values(("sku", "CapacityReservation"))
                }),
            DevId,
            descriptor);
        var capacityReservationLevel = capacityReservationValues.Single(value => value.Name == "capacityReservationLevel");
        capacityReservationLevel.Value!.Value.GetInt32().Should().Be(100);
        capacityReservationLevel.Provenance.Should().Be(EffectiveValueProvenance.CatalogDefault);
    }

    [Fact]
    public void HidesAdministratorLoginUnlessSqlAuthenticationRequiresIt()
    {
        var descriptor = Descriptor("SqlServer");

        var entraOnlyValues = EffectiveValueResolver.Resolve(
            Resource(type: "SqlServer"),
            DevId,
            descriptor);
        entraOnlyValues.Select(value => value.Name).Should().NotContain("administratorLogin");

        var entraAndSqlValues = EffectiveValueResolver.Resolve(
            Resource(
                type: "SqlServer",
                overrides: new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>
                {
                    [DevId] = Values(("authentication", "EntraEtSql"))
                }),
            DevId,
            descriptor);
        var administratorLogin = entraAndSqlValues.Single(value => value.Name == "administratorLogin");
        administratorLogin.Value!.Value.GetString().Should().Be("sqladmin");
        administratorLogin.Provenance.Should().Be(EffectiveValueProvenance.CatalogDefault);
    }

    [Fact]
    public void OverrideTakesPrecedenceOverResourceValue()
    {
        var resource = Resource(
            properties: Values(("purgeProtection", true)),
            overrides: new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>
            {
                [DevId] = Values(("purgeProtection", false))
            });

        var value = EffectiveValueResolver.Resolve(resource, DevId, KeyVaultDescriptor)
            .Single(candidate => candidate.Name == "purgeProtection");

        value.Value!.Value.GetBoolean().Should().BeFalse();
        value.Provenance.Should().Be(EffectiveValueProvenance.Override);
    }

    [Fact]
    public void ResourceValueTakesPrecedenceOverCatalogDefaultWhenOverrideIsRemoved()
    {
        var resource = Resource(properties: Values(("purgeProtection", false)));

        var value = EffectiveValueResolver.Resolve(resource, DevId, KeyVaultDescriptor)
            .Single(candidate => candidate.Name == "purgeProtection");

        value.Value!.Value.GetBoolean().Should().BeFalse();
        value.Provenance.Should().Be(EffectiveValueProvenance.Resource);
    }

    [Fact]
    public void PreservesAnExplicitlyEmptyStringAsAResourceValue()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"optionalText","default":"default","kind":"string"}]}""");
        var resource = Resource(properties: Values(("optionalText", string.Empty)));

        var value = EffectiveValueResolver.Resolve(resource, DevId, descriptor.RootElement)
            .Single(candidate => candidate.Name == "optionalText");

        value.Value!.Value.GetString().Should().BeEmpty();
        value.Provenance.Should().Be(EffectiveValueProvenance.Resource);
    }

    [Fact]
    public void ExplicitAbsentValueDoesNotFallThroughToCatalogDefault()
    {
        using var descriptor = JsonDocument.Parse(
            """{"properties":[{"name":"optionalText","default":"default","kind":"string"}]}""");
        var resource = Resource(properties: Values(("optionalText", (string?)null)));

        var value = EffectiveValueResolver.Resolve(resource, DevId, descriptor.RootElement)
            .Single(candidate => candidate.Name == "optionalText");

        value.Value.Should().BeNull();
        value.Provenance.Should().Be(EffectiveValueProvenance.Absent);
    }

    [Fact]
    public void ExistingResourceHasNoDeployableEffectiveProperties()
    {
        var resource = Resource(isExisting: true);

        var values = EffectiveValueResolver.Resolve(resource, DevId, KeyVaultDescriptor);

        values.Should().BeEmpty();
    }

    [Fact]
    public void ResolvesTheReferencePilotValuesForLogsKeyVaultAndContainerApp()
    {
        var logDescriptor = Descriptor("LogAnalyticsWorkspace");
        var log = Resource(
            type: "LogAnalyticsWorkspace",
            properties: Values(("retentionInDays", 30)),
            overrides: new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>
            {
                [Guid.Parse("22222222-2222-4222-8222-222222222222")] = Values(("retentionInDays", 90))
            });
        var logDev = EffectiveValueResolver.Resolve(log, DevId, logDescriptor)
            .Single(value => value.Name == "retentionInDays");
        var logPrd = EffectiveValueResolver.Resolve(log, Guid.Parse("22222222-2222-4222-8222-222222222222"), logDescriptor)
            .Single(value => value.Name == "retentionInDays");
        logDev.Value!.Value.GetInt32().Should().Be(30);
        logDev.Provenance.Should().Be(EffectiveValueProvenance.Resource);
        logPrd.Value!.Value.GetInt32().Should().Be(90);
        logPrd.Provenance.Should().Be(EffectiveValueProvenance.Override);

        var keyVault = Resource(
            properties: Values(("purgeProtection", true)),
            overrides: new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>
            {
                [DevId] = Values(("purgeProtection", false))
            });
        var keyVaultDev = EffectiveValueResolver.Resolve(keyVault, DevId, KeyVaultDescriptor)
            .Single(value => value.Name == "purgeProtection");
        var keyVaultPrd = EffectiveValueResolver.Resolve(
                keyVault,
                Guid.Parse("22222222-2222-4222-8222-222222222222"),
                KeyVaultDescriptor)
            .Single(value => value.Name == "purgeProtection");
        keyVaultDev.Value!.Value.GetBoolean().Should().BeFalse();
        keyVaultDev.Provenance.Should().Be(EffectiveValueProvenance.Override);
        keyVaultPrd.Value!.Value.GetBoolean().Should().BeTrue();
        keyVaultPrd.Provenance.Should().Be(EffectiveValueProvenance.Resource);

        var appDescriptor = Descriptor("ContainerApp");
        var app = Resource(
            type: "ContainerApp",
            properties: Values(("cpu", 0.5), ("minReplicas", 0), ("maxReplicas", 2)),
            overrides: new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>
            {
                [Guid.Parse("22222222-2222-4222-8222-222222222222")] = Values(
                    ("cpu", 1),
                    ("minReplicas", 1),
                    ("maxReplicas", 3))
            });
        var appDev = EffectiveValueResolver.Resolve(app, DevId, appDescriptor);
        var appPrd = EffectiveValueResolver.Resolve(app, Guid.Parse("22222222-2222-4222-8222-222222222222"), appDescriptor);
        appDev.Single(value => value.Name == "cpu").Value!.Value.GetDouble().Should().Be(0.5);
        appPrd.Single(value => value.Name == "cpu").Value!.Value.GetDouble().Should().Be(1);
        appDev.Single(value => value.Name == "minReplicas").Value!.Value.GetInt32().Should().Be(0);
        appDev.Single(value => value.Name == "maxReplicas").Value!.Value.GetInt32().Should().Be(2);
        appPrd.Single(value => value.Name == "minReplicas").Value!.Value.GetInt32().Should().Be(1);
        appPrd.Single(value => value.Name == "maxReplicas").Value!.Value.GetInt32().Should().Be(3);
    }

    private static ResourceSnapshot Resource(
        IReadOnlyDictionary<string, JsonElement>? properties = null,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, JsonElement>>? overrides = null,
        bool isExisting = false,
        string type = "KeyVault") => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        type,
        "main",
        string.Empty,
        isExisting,
        properties ?? Values(),
        overrides ?? new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(),
        new Dictionary<Guid, bool>(),
        new Dictionary<Guid, string>(),
        new Dictionary<Guid, string>(),
        new Dictionary<string, string>(),
        []);

    private static Dictionary<string, JsonElement> Values(params (string Name, object? Value)[] entries) =>
        entries.ToDictionary(entry => entry.Name, entry => JsonSerializer.SerializeToElement(entry.Value), StringComparer.Ordinal);

    private static JsonElement Descriptor(string type) => Catalog
        .GetVersion("2026.10-pilot")!
        .GetResourceType(type)!
        .Definition;
}
