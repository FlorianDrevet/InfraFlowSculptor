using System.Text.Json;
using System.Reflection;
using ErrorOr;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;

namespace InfraFlowSculptor.Application.Tests.Components;

public sealed class PropertyValueValidatorTests
{
    private static readonly CatalogResourceTypeDescriptor Descriptor =
        CatalogLoader.Load().LatestPublished!.GetResourceType("ContainerAppsEnvironment")!;

    private static readonly CatalogResourceTypeDescriptor KeyVaultDescriptor =
        CatalogLoader.Load().LatestPublished!.GetResourceType("KeyVault")!;

    private static readonly CatalogResourceTypeDescriptor LogAnalyticsDescriptor =
        CatalogLoader.Load().LatestPublished!.GetResourceType("LogAnalyticsWorkspace")!;

    private static readonly CatalogResourceTypeDescriptor SqlServerDescriptor =
        CatalogLoader.Load().LatestPublished!.GetResourceType("SqlServer")!;

    [Fact]
    public void AcceptsDescriptorDrivenScalarAndTypedArrayValues()
    {
        using var scalar = JsonDocument.Parse("false");
        using var profiles = JsonDocument.Parse("""
            [{"name":"D4","workloadProfileType":"D4","minimumCount":1,"maximumCount":2}]
            """);
        using var nested = JsonDocument.Parse("""[{"limits":{"cpu":1.5},"labels":["api","worker"]}]""");
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["zoneRedundant"] = scalar.RootElement.Clone(),
            ["workloadProfiles"] = profiles.RootElement.Clone()
        };
        var arbitraryStructured = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["workloadProfiles"] = nested.RootElement.Clone()
        };

        Assert.False(PropertyValueValidator.Validate(Descriptor, properties).IsError);
        Assert.True(PropertyValueValidator.Validate(Descriptor, arbitraryStructured).IsError);
    }

    [Fact]
    public void AcceptsNumericEnumValuesDeclaredByLogAnalyticsDescriptor()
    {
        using var capacity = JsonDocument.Parse("100");

        var result = PropertyValueValidator.Validate(LogAnalyticsDescriptor, new Dictionary<string, JsonElement>
        {
            ["capacityReservationLevel"] = capacity.RootElement.Clone()
        });

        Assert.False(result.IsError);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Admin")]
    [InlineData("administrator")]
    [InlineData("SA")]
    [InlineData("root")]
    [InlineData("dbmanager")]
    [InlineData("loginmanager")]
    [InlineData("guest")]
    [InlineData("PUBLIC")]
    [InlineData("sqladmin\n")]
    public void RejectsDocumentedReservedSqlAdministratorLoginNamesRegardlessOfCase(string login)
    {
        using var value = JsonDocument.Parse(JsonSerializer.Serialize(login));

        var result = PropertyValueValidator.Validate(SqlServerDescriptor, new Dictionary<string, JsonElement>
        {
            ["administratorLogin"] = value.RootElement.Clone()
        });

        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_PATTERN");
    }

    [Fact]
    public void RejectsUnknownMembersInTypedWorkloadProfileItems()
    {
        using var profiles = JsonDocument.Parse("""
            [{"name":"D4","workloadProfileType":"D4","minimumCount":0,"maximumCount":4,"unexpected":true}]
            """);

        var result = PropertyValueValidator.Validate(TypedWorkloadProfilesDescriptor, new Dictionary<string, JsonElement>
        {
            ["workloadProfiles"] = profiles.RootElement.Clone()
        });

        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_OBJECT_MEMBER_UNKNOWN");
    }

    [Fact]
    public void RejectsDuplicateMembersInTypedWorkloadProfileItems()
    {
        using var profiles = JsonDocument.Parse("""
            [{"name":"D4","name":"E4","workloadProfileType":"D4","minimumCount":0,"maximumCount":4}]
            """);

        var result = PropertyValueValidator.Validate(TypedWorkloadProfilesDescriptor, new Dictionary<string, JsonElement>
        {
            ["workloadProfiles"] = profiles.RootElement.Clone()
        });

        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_OBJECT_MEMBER_DUPLICATE");
    }

    [Fact]
    public void DoesNotEchoUntrustedUnknownMemberNamesInPropertyErrors()
    {
        using var profiles = JsonDocument.Parse("""
            [{"bad\u202Ekey":true,"name":"D4","workloadProfileType":"D4","minimumCount":0,"maximumCount":4}]
            """);

        var result = PropertyValueValidator.Validate(TypedWorkloadProfilesDescriptor, new Dictionary<string, JsonElement>
        {
            ["workloadProfiles"] = profiles.RootElement.Clone()
        });
        var error = Assert.Single(result.Errors);

        Assert.Equal("RESOURCE_PROPERTY_OBJECT_MEMBER_UNKNOWN", error.Code);
        Assert.DoesNotContain('\u202E', error.Description);
    }

    [Fact]
    public void RejectsDuplicateNamesInTypedWorkloadProfileItems()
    {
        using var profiles = JsonDocument.Parse("""
            [
            {"name":"D4","workloadProfileType":"D4","minimumCount":0,"maximumCount":4},
            {"name":"D4","workloadProfileType":"D8","minimumCount":1,"maximumCount":8}
            ]
            """);

        var result = PropertyValueValidator.Validate(TypedWorkloadProfilesDescriptor, new Dictionary<string, JsonElement>
        {
            ["workloadProfiles"] = profiles.RootElement.Clone()
        });

        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_ARRAY_ITEM_DUPLICATE");
    }

    [Fact]
    public void RejectsConsumptionAsADedicatedWorkloadProfileName()
    {
        using var profiles = JsonDocument.Parse("""
            [{"name":"Consumption","workloadProfileType":"D4","minimumCount":0,"maximumCount":4}]
            """);

        var result = PropertyValueValidator.Validate(TypedWorkloadProfilesDescriptor, new Dictionary<string, JsonElement>
        {
            ["workloadProfiles"] = profiles.RootElement.Clone()
        });

        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_VALUE_FORBIDDEN");
    }

    [Fact]
    public void RejectsMissingRequiredWorkloadProfileMembers()
    {
        using var profiles = JsonDocument.Parse("""
            [{"name":"D4","workloadProfileType":"D4","minimumCount":0}]
            """);

        var result = PropertyValueValidator.Validate(TypedWorkloadProfilesDescriptor, new Dictionary<string, JsonElement>
        {
            ["workloadProfiles"] = profiles.RootElement.Clone()
        });

        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_OBJECT_MEMBER_REQUIRED");
    }

    [Fact]
    public void RejectsNullValuesForRequiredTypedWorkloadProfileMembers()
    {
        using var profiles = JsonDocument.Parse("""
            [{"name":null,"workloadProfileType":"D4","minimumCount":null,"maximumCount":4}]
            """);

        var result = PropertyValueValidator.Validate(TypedWorkloadProfilesDescriptor, new Dictionary<string, JsonElement>
        {
            ["workloadProfiles"] = profiles.RootElement.Clone()
        });

        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_OBJECT_MEMBER_REQUIRED");
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("10.12.0.0/16")]
    [InlineData("255.255.255.255/32")]
    public void AcceptsCanonicalIpv4CidrRanges(string cidr)
    {
        var result = ValidateCidrRanges(cidr);

        Assert.False(result.IsError);
    }

    [Fact]
    public void AcceptsAnEmptyRangeListUntilNetworkFindingsAreImplemented()
    {
        var result = ValidateCidrRanges();

        Assert.False(result.IsError);
    }

    [Theory]
    [InlineData("2001:db8::/32")]
    [InlineData("256.1.2.3/24")]
    [InlineData("10.0.0.0/33")]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.1/24")]
    [InlineData("010.0.0.0/8")]
    public void RejectsInvalidIpv4CidrRanges(string cidr)
    {
        var result = ValidateCidrRanges(cidr);

        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_FORMAT");
    }

    [Fact]
    public void LimitsCidrRangesToOneHundredWithoutRequiringOneYet()
    {
        var hundred = ValidateCidrRanges(Enumerable.Repeat("10.0.0.0/8", 100).ToArray());
        var oneHundredAndOne = ValidateCidrRanges(Enumerable.Repeat("10.0.0.0/8", 101).ToArray());

        Assert.False(hundred.IsError);
        Assert.Contains(oneHundredAndOne.Errors, error => error.Code == "RESOURCE_PROPERTY_ARRAY_LENGTH");
    }

    [Fact]
    public void RejectsNullArrayElementsWhenAnItemDescriptorIsPresent()
    {
        using var nullElement = JsonDocument.Parse("[null]");
        var result = PropertyValueValidator.Validate(CidrDescriptor, new Dictionary<string, JsonElement>
        {
            ["allowedIpRanges"] = nullElement.RootElement.Clone()
        });

        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_ITEM_TYPE_INVALID");
    }

    [Fact]
    public void RejectsUnknownNamesWrongTypesAndNonOverridableProperties()
    {
        using var wrongType = JsonDocument.Parse("\"false\"");
        using var valid = JsonDocument.Parse("true");
        var unknown = PropertyValueValidator.Validate(Descriptor, new Dictionary<string, JsonElement>
        {
            ["notAProperty"] = valid.RootElement.Clone()
        });
        var wrong = PropertyValueValidator.Validate(Descriptor, new Dictionary<string, JsonElement>
        {
            ["zoneRedundant"] = wrongType.RootElement.Clone()
        });
        var nonOverridable = PropertyValueValidator.ValidateOverride(Descriptor, "zoneRedundant", valid.RootElement.Clone());

        Assert.Contains(unknown.Errors, error => error.Code == "RESOURCE_PROPERTY_UNKNOWN");
        Assert.Contains(wrong.Errors, error => error.Code == "RESOURCE_PROPERTY_TYPE_INVALID");
        Assert.Contains(nonOverridable.Errors, error => error.Code == "RESOURCE_PROPERTY_NOT_OVERRIDABLE");
    }

    [Fact]
    public void RejectsAChangeToALockedPropertyAfterPublication()
    {
        using var previous = JsonDocument.Parse("false");
        using var changed = JsonDocument.Parse("true");
        var result = PropertyValueValidator.Validate(
            Descriptor,
            new Dictionary<string, JsonElement> { ["zoneRedundant"] = changed.RootElement.Clone() },
            isPublished: true,
            currentValues: new Dictionary<string, JsonElement> { ["zoneRedundant"] = previous.RootElement.Clone() });

        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_LOCKED");
    }

    [Fact]
    public void ResolvesDescriptorDefaultsBeforeBaseValuesAndTargetOverrides()
    {
        using var baseProtection = JsonDocument.Parse("false");
        using var targetProtection = JsonDocument.Parse("true");
        var baseValues = new Dictionary<string, JsonElement>
        {
            ["purgeProtection"] = baseProtection.RootElement.Clone()
        };
        var targetValues = new Dictionary<string, JsonElement>
        {
            ["purgeProtection"] = targetProtection.RootElement.Clone()
        };

        var inherited = PropertyValueValidator.ResolveEffectiveValues(KeyVaultDescriptor, baseValues);
        var target = PropertyValueValidator.ResolveEffectiveValues(KeyVaultDescriptor, baseValues, targetValues);

        Assert.True(inherited["purgeProtection"].ValueKind == JsonValueKind.False);
        Assert.True(target["purgeProtection"].ValueKind == JsonValueKind.True);
        Assert.Equal(90, inherited["softDeleteRetentionInDays"].GetInt32());
    }

    [Fact]
    public void RejectsClearingAnOverrideThatReversesAnIrreversibleTransition()
    {
        using var baseProtection = JsonDocument.Parse("false");
        using var targetProtection = JsonDocument.Parse("true");
        var baseValues = new Dictionary<string, JsonElement>
        {
            ["purgeProtection"] = baseProtection.RootElement.Clone()
        };
        var targetValues = new Dictionary<string, JsonElement>
        {
            ["purgeProtection"] = targetProtection.RootElement.Clone()
        };

        var result = PropertyValueValidator.ValidateOverrideClear(
            KeyVaultDescriptor,
            "purgeProtection",
            PropertyValueValidator.ResolveEffectiveValues(KeyVaultDescriptor, baseValues, targetValues),
            PropertyValueValidator.ResolveEffectiveValues(KeyVaultDescriptor, baseValues));

        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_IRREVERSIBLE");
    }

    [Fact]
    public void RejectsChangingFromADescriptorDefaultAfterPublication()
    {
        using var changedValue = JsonDocument.Parse("30");
        var result = PropertyValueValidator.Validate(
            KeyVaultDescriptor,
            new Dictionary<string, JsonElement>
            {
                ["softDeleteRetentionInDays"] = changedValue.RootElement.Clone()
            },
            isPublished: true,
            currentValues: PropertyValueValidator.ResolveEffectiveValues(KeyVaultDescriptor));

        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_LOCKED");
    }

    [Fact]
    public void EnforcesTheOptionalDescriptorPattern()
    {
        using var definition = JsonDocument.Parse("""
            {
              "type": "PatternTest",
              "category": "compute",
              "icon": "server",
              "labels": { "fr": "Test", "en": "Test" },
              "naming": { "abbreviation": "pt" },
              "properties": [
                { "name": "slug", "kind": "string", "overridable": true, "pattern": "^[a-z0-9-]+$" }
              ],
              "children": []
            }
            """);
        var constructor = typeof(CatalogResourceTypeDescriptor).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(JsonElement)],
            modifiers: null);
        Assert.NotNull(constructor);
        var descriptor = Assert.IsType<CatalogResourceTypeDescriptor>(constructor.Invoke([definition.RootElement]));
        using var invalidValue = JsonDocument.Parse("\"UpperCase\"");

        var result = PropertyValueValidator.Validate(
            descriptor,
            new Dictionary<string, JsonElement> { ["slug"] = invalidValue.RootElement.Clone() });

        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_PATTERN");
    }

    private static readonly CatalogResourceTypeDescriptor TypedWorkloadProfilesDescriptor = CreateDescriptor("""
        {
          "type": "TypedWorkloadProfilesTest",
          "category": "compute",
          "icon": "container-apps-environment",
          "labels": { "fr": "Profils de charge", "en": "Workload profiles" },
          "naming": { "abbreviation": "wp" },
          "properties": [
            {
              "name": "workloadProfiles",
              "kind": "array",
              "overridable": true,
              "default": [],
              "items": {
                "kind": "object",
                "uniqueBy": "name",
                "members": [
                  { "name": "name", "kind": "string", "required": true, "minLength": 1, "forbiddenValues": ["Consumption"] },
                  { "name": "workloadProfileType", "kind": "enum", "required": true, "values": ["D4", "D8"] },
                  { "name": "minimumCount", "kind": "integer", "required": true, "minimum": 0 },
                  { "name": "maximumCount", "kind": "integer", "required": true, "minimum": 0 }
                ]
              }
            }
          ]
        }
        """);

    private static readonly CatalogResourceTypeDescriptor CidrDescriptor = CreateDescriptor("""
        {
          "type": "CidrTest",
          "category": "security",
          "icon": "network",
          "labels": { "fr": "Test CIDR", "en": "CIDR Test" },
          "naming": { "abbreviation": "ct" },
          "properties": [
            {
              "name": "allowedIpRanges",
              "kind": "array",
              "overridable": true,
              "default": [],
              "items": { "kind": "string", "format": "ipv4Cidr" },
              "maxItems": 100
            }
          ]
        }
        """);

    private static ErrorOr<Success> ValidateCidrRanges(params string[] cidrs)
    {
        var values = JsonSerializer.SerializeToElement(cidrs);
        return PropertyValueValidator.Validate(CidrDescriptor, new Dictionary<string, JsonElement>
        {
            ["allowedIpRanges"] = values
        });
    }

    private static CatalogResourceTypeDescriptor CreateDescriptor(string json)
    {
        using var definition = JsonDocument.Parse(json);
        var constructor = typeof(CatalogResourceTypeDescriptor).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(JsonElement)],
            modifiers: null);
        Assert.NotNull(constructor);
        return Assert.IsType<CatalogResourceTypeDescriptor>(constructor.Invoke([definition.RootElement.Clone()]));
    }
}
