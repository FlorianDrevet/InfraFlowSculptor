using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using InfraFlowSculptor.Catalog;
using Json.Schema;

namespace InfraFlowSculptor.Catalog.Tests;

public sealed class CatalogSchemaTests
{
    private static readonly string CatalogRoot = Path.Combine(AppContext.BaseDirectory, "catalog");
    private static readonly string PilotRoot = Path.Combine(CatalogRoot, "2026.10-pilot");

    private static IEnumerable<string> CatalogVersionDirectories() => CatalogVersionDirectories(CatalogRoot);

    private static IEnumerable<string> CatalogVersionDirectories(string catalogRoot)
    {
        foreach (var path in Directory.GetDirectories(catalogRoot, "*", SearchOption.TopDirectoryOnly)
                     .Where(path => !string.Equals(Path.GetFileName(path), "schema", StringComparison.Ordinal))
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            Assert.True(File.Exists(Path.Combine(path, "catalog.json")),
                $"Catalog version directory '{Path.GetFileName(path)}' is missing catalog.json.");
            yield return path;
        }
    }

    [Fact]
    public void DraftPilotPatchDefinesDeclarativeBicepMetadata()
    {
        var catalog = CatalogLoader.Load(typeof(CatalogLoader).Assembly)
            .GetVersion("2026.10-pilot.6");

        Assert.NotNull(catalog);
        Assert.Equal("draft", catalog!.Status);
        var registry = catalog.GetResourceType("ContainerRegistry");
        Assert.NotNull(registry);
        Assert.True(registry!.Definition.GetProperty("generation").GetProperty("Bicep")
            .TryGetProperty("symbol", out _));
        Assert.Contains(catalog.Roles, role => role.Name == "Container Apps Contributor"
            && role.DefinitionId == "358470bc-b998-42bd-ab17-a7e34c199c0f");
        Assert.Equal("2026.10-pilot.5", CatalogLoader.Load(typeof(CatalogLoader).Assembly).LatestPublished?.Version);
        var app = catalog.GetResourceType("ContainerApp")!.Definition
            .GetProperty("generation").GetProperty("Bicep").GetProperty("config");
        var cpu = app.GetProperty("fields").EnumerateArray()
            .Single(field => field.GetProperty("name").GetString() == "cpu");
        Assert.Equal("numberToString", cpu.GetProperty("transform").GetString());
        var database = catalog.GetResourceType("SqlServer")!.Definition
            .GetProperty("generation").GetProperty("Bicep").GetProperty("embeddedChildren");
        Assert.Equal("databases", Assert.Single(database.EnumerateArray()).GetProperty("collection").GetString());
    }

    [Fact]
    public void CatalogVersionSchemaAcceptsPatchReleaseOfPilot()
    {
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(CatalogRoot, "schema", "catalog-version.schema.json")));
        using var candidate = JsonDocument.Parse("""
            {
              "version": "2026.10-pilot.2",
              "status": "published",
              "publishedAt": "2026-10-29",
              "supportEndsAt": null,
              "notes": "Correctif du catalogue pilote.",
              "verifiedAgainstDocsOn": "2026-10-29"
            }
            """);

        var result = schema.Evaluate(candidate.RootElement);

        Assert.True(result.IsValid, result.ToString());
    }

    [Fact]
    public void CatalogVersionDirectoryWithoutMetadataFailsDiscovery()
    {
        var catalogRoot = Path.Combine(Path.GetTempPath(), $"ifs-catalog-schema-{Guid.NewGuid():N}");
        var incompleteVersion = Path.Combine(catalogRoot, "2026.10-pilot.99");
        Directory.CreateDirectory(incompleteVersion);

        try
        {
            var exception = Record.Exception(() => CatalogVersionDirectories(catalogRoot).ToArray());

            Assert.NotNull(exception);
            Assert.Contains("catalog.json", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(catalogRoot, recursive: true);
        }
    }

    [Fact]
    public void CatalogVersionDirectoriesIncludeReleasePeriodsBeyondTheCurrentMonth()
    {
        var catalogRoot = Path.Combine(Path.GetTempPath(), $"ifs-catalog-future-{Guid.NewGuid():N}");
        var futureVersion = Path.Combine(catalogRoot, "2026.11-pilot");
        Directory.CreateDirectory(futureVersion);
        File.WriteAllText(Path.Combine(futureVersion, "catalog.json"), "{}");

        try
        {
            var versions = CatalogVersionDirectories(catalogRoot).ToArray();

            Assert.Equal([futureVersion], versions);
        }
        finally
        {
            Directory.Delete(catalogRoot, recursive: true);
        }
    }

    [Fact]
    public void ResourcePropertyItemsAreOnlyAllowedForArrayProperties()
    {
        var descriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(CatalogRoot, "2026.10-pilot.4", "types", "ContainerRegistry.json")))!
            .AsObject();
        var allowedRanges = descriptor["properties"]!.AsArray()
            .Single(property => property!["name"]!.GetValue<string>() == "allowedIpRanges")!
            .AsObject();
        allowedRanges["kind"] = "integer";

        Assert.False(IsValidResourceDescriptor(descriptor));
    }

    [Fact]
    public void PropertyMemberFormatRequiresStringKind()
    {
        var descriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(
            CatalogRoot,
            "2026.10-pilot.5",
            "types",
            "ContainerAppsEnvironment.json")))!.AsObject();
        var profiles = descriptor["properties"]!.AsArray()
            .Single(property => property!["name"]!.GetValue<string>() == "workloadProfiles")!["items"]!.AsObject();
        var member = profiles["members"]!.AsArray()
            .Single(value => value!["name"]!.GetValue<string>() == "name")!.AsObject();
        member["kind"] = "integer";
        member["format"] = "ipv4Cidr";

        Assert.False(IsValidResourceDescriptor(descriptor));
    }

    [Fact]
    public void ResourceDescriptorSchemaAcceptsTypedWorkloadProfilesAndDeclarativeConstraints()
    {
        var environment = JsonNode.Parse(File.ReadAllText(Path.Combine(
            CatalogRoot, "2026.10-pilot.4", "types", "ContainerAppsEnvironment.json")))!.AsObject();
        var workloadProfiles = environment["properties"]!.AsArray()
            .Single(property => property!["name"]!.GetValue<string>() == "workloadProfiles")!.AsObject();
        workloadProfiles["items"] = JsonNode.Parse("""
            {
              "kind": "object",
              "uniqueBy": "name",
              "members": [
                { "name": "name", "kind": "string", "required": true, "minLength": 1 },
                { "name": "type", "kind": "enum", "required": true, "values": ["D4", "D8"] },
                { "name": "minInstances", "kind": "integer", "required": true, "minimum": 0 },
                { "name": "maxInstances", "kind": "integer", "required": true, "minimum": 0 }
              ],
              "constraints": [
                { "id": "profile-range", "kind": "compare", "left": "minInstances", "operator": "lessThanOrEqual", "right": "maxInstances" }
              ]
            }
            """);

        var app = JsonNode.Parse(File.ReadAllText(Path.Combine(
            CatalogRoot, "2026.10-pilot.4", "types", "ContainerApp.json")))!.AsObject();
        var profileName = app["properties"]!.AsArray()
            .Single(property => property!["name"]!.GetValue<string>() == "workloadProfileName")!.AsObject();
        profileName["allowedFrom"] = JsonNode.Parse("""
            {
              "linkKind": "Hosting",
              "targetProperty": "workloadProfiles",
              "targetMember": "name",
              "implicitValues": ["Consumption"]
            }
            """);
        app["constraints"] = JsonNode.Parse("""
            [
              { "id": "replica-range", "kind": "compare", "left": "minReplicas", "operator": "lessThanOrEqual", "right": "maxReplicas" },
              {
                "id": "consumption-cpu-memory",
                "kind": "allowedTuples",
                "properties": ["cpu", "memoryGi"],
                "tuples": [[0.25, "0.5Gi"], [0.5, "1Gi"], [1, "2Gi"]],
                "when": { "property": "workloadProfileName", "equals": "Consumption" }
              }
            ]
            """);

        Assert.True(IsValidResourceDescriptor(environment), "The typed workload profile descriptor must match the closed schema.");
        Assert.True(IsValidResourceDescriptor(app), "The property reference and cross-property constraints must match the closed schema.");
    }

    [Fact]
    public void ResourceConstraintSchemaRejectsUnknownConstraintKinds()
    {
        var descriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(
            CatalogRoot, "2026.10-pilot.5", "types", "ContainerApp.json")))!.AsObject();
        descriptor["constraints"]!.AsArray()[0]!["kind"] = "resourceTypeSpecific";

        Assert.False(IsValidResourceDescriptor(descriptor));
    }

    [Fact]
    public void ResourceConstraintSchemaBoundsIdsToKeepItemFindingKeysWithinTheSupportedLength()
    {
        var descriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(
            CatalogRoot, "2026.10-pilot.5", "types", "ContainerApp.json")))!.AsObject();
        descriptor["constraints"]!.AsArray()[0]!["id"] = new string('x', 47);

        Assert.True(IsValidResourceDescriptor(descriptor));

        descriptor["constraints"]!.AsArray()[0]!["id"] = new string('x', 48);

        Assert.False(IsValidResourceDescriptor(descriptor));

        descriptor["constraints"]!.AsArray()[0]!["id"] = new string('x', 46) + "\n";

        Assert.False(IsValidResourceDescriptor(descriptor));
    }

    [Fact]
    public void ResourceConstraintSemanticAuditRejectsDuplicateIdsInTheSameScope()
    {
        var descriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(
            CatalogRoot, "2026.10-pilot.5", "types", "ContainerApp.json")))!.AsObject();
        var constraints = descriptor["constraints"]!.AsArray();
        constraints[1]!["id"] = constraints[0]!["id"]!.GetValue<string>();

        Assert.False(IsValidResourceDescriptor(descriptor));
    }

    [Fact]
    public void ResourceConstraintSchemaRejectsUnsafeIdsUsedInMessagesAndFindingKeys()
    {
        var descriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(
            CatalogRoot, "2026.10-pilot.5", "types", "ContainerApp.json")))!.AsObject();
        descriptor["constraints"]!.AsArray()[0]!["id"] = "invalid\nidentifier";

        Assert.False(IsValidResourceDescriptor(descriptor));
    }

    [Fact]
    public void ResourceConstraintSchemaRejectsUnlistedConstraintFields()
    {
        var descriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(
            CatalogRoot, "2026.10-pilot.5", "types", "ContainerApp.json")))!.AsObject();
        descriptor["constraints"]!.AsArray()[0]!["resourceType"] = "ContainerApp";

        Assert.False(IsValidResourceDescriptor(descriptor));
    }

    [Fact]
    public void ResourceConstraintSemanticAuditRejectsTuplesWithWrongArity()
    {
        var descriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(
            CatalogRoot, "2026.10-pilot.5", "types", "ContainerApp.json")))!.AsObject();
        descriptor["constraints"]!.AsArray()
            .Single(constraint => constraint!["kind"]!.GetValue<string>() == "allowedTuples")!["tuples"]!.AsArray()[0]!.AsArray()
            .Add(JsonValue.Create("unexpected"));

        Assert.False(IsValidResourceDescriptor(descriptor));
    }

    [Fact]
    public void ResourceConstraintSemanticAuditRejectsTupleValuesOutsideReferencedProperty()
    {
        var descriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(
            CatalogRoot, "2026.10-pilot.5", "types", "ContainerApp.json")))!.AsObject();
        descriptor["constraints"]!.AsArray()
            .Single(constraint => constraint!["kind"]!.GetValue<string>() == "allowedTuples")!["tuples"]!.AsArray()[0]!.AsArray()[0] =
            JsonValue.Create("0.25");

        Assert.False(IsValidResourceDescriptor(descriptor));
    }

    [Fact]
    public void ResourceConstraintSemanticAuditRequiresComparedPropertiesToBeNumeric()
    {
        var descriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(
            CatalogRoot, "2026.10-pilot.5", "types", "ContainerApp.json")))!.AsObject();
        descriptor["constraints"]!.AsArray()[0]!["left"] = JsonValue.Create("workloadProfileName");

        Assert.False(IsValidResourceDescriptor(descriptor));
    }

    [Fact]
    public void Ipv4CidrItemsMustBeStrings()
    {
        var descriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(CatalogRoot, "2026.10-pilot.4", "types", "ContainerRegistry.json")))!
            .AsObject();
        var allowedRanges = descriptor["properties"]!.AsArray()
            .Single(property => property!["name"]!.GetValue<string>() == "allowedIpRanges")!
            .AsObject();
        allowedRanges["items"]!["kind"] = "integer";

        Assert.False(IsValidResourceDescriptor(descriptor));
    }

    [Fact]
    public void ContainerRegistryDisablesAnonymousPullAndDoesNotDirectlyMapRoleAssignmentMode()
    {
        var provider = CatalogLoader.Load(typeof(CatalogLoader).Assembly);
        var descriptor = provider.LatestPublished!.GetResourceType("ContainerRegistry")!.Definition;
        var anonymousPull = Assert.Single(descriptor.GetProperty("fixedValues").EnumerateArray());
        var roleAssignmentMode = descriptor.GetProperty("properties").EnumerateArray()
            .Single(property => property.GetProperty("name").GetString() == "roleAssignmentMode");

        Assert.Equal("anonymousPullEnabled", anonymousPull.GetProperty("name").GetString());
        Assert.False(anonymousPull.GetProperty("value").GetBoolean());
        Assert.Equal("Registry", roleAssignmentMode.GetProperty("default").GetString());
        var roleAssignmentModes = roleAssignmentMode.GetProperty("values").EnumerateArray()
            .Select(value => value.GetString()).ToArray();
        Assert.Equal(2, roleAssignmentModes.Length);
        Assert.Equal("Registry", roleAssignmentModes[0]);
        Assert.Equal("Repository", roleAssignmentModes[1]);

        var mappings = descriptor.GetProperty("generation").GetProperty("Bicep").GetProperty("map");
        Assert.False(mappings.TryGetProperty("roleAssignmentMode", out _),
            "Registry/Repository are IFS values and need an explicit conversion before mapping to the AVM enum.");
    }

    [Fact]
    public void ContainerAppReplicaPropertiesDeclareTheAzureMaximum()
    {
        var provider = CatalogLoader.Load(typeof(CatalogLoader).Assembly);
        var descriptor = provider.LatestPublished!.GetResourceType("ContainerApp")!.Definition;
        var properties = descriptor.GetProperty("properties").EnumerateArray()
            .ToDictionary(property => property.GetProperty("name").GetString()!, StringComparer.Ordinal);

        Assert.Equal(1000, properties["minReplicas"].GetProperty("maximum").GetInt32());
        Assert.Equal(1000, properties["maxReplicas"].GetProperty("maximum").GetInt32());
    }

    [Fact]
    public void LatestCatalogDeclaresTypedWorkloadProfilesAndContainerAppCrossConstraints()
    {
        var catalog = CatalogLoader.Load(typeof(CatalogLoader).Assembly).LatestPublished!;
        Assert.Equal("2026.10-pilot.5", catalog.Version);

        var environment = catalog.GetResourceType("ContainerAppsEnvironment")!.Definition;
        var profiles = environment.GetProperty("properties").EnumerateArray()
            .Single(property => property.GetProperty("name").GetString() == "workloadProfiles");
        var profileItems = profiles.GetProperty("items");
        Assert.Equal("object", profileItems.GetProperty("kind").GetString());
        Assert.Equal("name", profileItems.GetProperty("uniqueBy").GetString());
        var profileMembers = profileItems.GetProperty("members").EnumerateArray()
            .ToDictionary(member => member.GetProperty("name").GetString()!, StringComparer.Ordinal);
        Assert.Equal("maximumCount,minimumCount,name,workloadProfileType", string.Join(',', profileMembers.Keys.Order(StringComparer.Ordinal)));
        Assert.True(profileMembers.Values.All(member => member.GetProperty("required").GetBoolean()));
        Assert.Equal("Consumption", profileMembers["name"].GetProperty("forbiddenValues")[0].GetString());
        Assert.Equal("D4,D8,D16,D32,E4,E8,E16,E32", string.Join(',',
            profileMembers["workloadProfileType"].GetProperty("values").EnumerateArray().Select(value => value.GetString())));
        Assert.Equal("lessThanOrEqual", profileItems.GetProperty("constraints")[0].GetProperty("operator").GetString());
        Assert.Equal("minimumCount", profileItems.GetProperty("constraints")[0].GetProperty("left").GetString());
        Assert.Equal("maximumCount", profileItems.GetProperty("constraints")[0].GetProperty("right").GetString());

        var app = catalog.GetResourceType("ContainerApp")!.Definition;
        var appProperties = app.GetProperty("properties").EnumerateArray()
            .ToDictionary(property => property.GetProperty("name").GetString()!, StringComparer.Ordinal);
        var profileSource = appProperties["workloadProfileName"].GetProperty("allowedFrom");
        Assert.Equal("Hosting", profileSource.GetProperty("linkKind").GetString());
        Assert.Contains(app.GetProperty("links").GetProperty("asSource").EnumerateArray(), linkKind =>
            linkKind.GetString() == profileSource.GetProperty("linkKind").GetString());
        Assert.Equal("workloadProfiles", profileSource.GetProperty("targetProperty").GetString());
        Assert.Equal("name", profileSource.GetProperty("targetMember").GetString());
        Assert.Contains(profileSource.GetProperty("implicitValues").EnumerateArray(), value => value.GetString() == "Consumption");
        var constraints = app.GetProperty("constraints").EnumerateArray()
            .ToDictionary(constraint => constraint.GetProperty("id").GetString()!, StringComparer.Ordinal);
        Assert.Equal(2, constraints.Count);
        Assert.Equal("minReplicas", constraints["replica-range"].GetProperty("left").GetString());
        Assert.Equal("maxReplicas", constraints["replica-range"].GetProperty("right").GetString());
        Assert.Equal("cpu,memoryGi", string.Join(',',
            constraints["consumption-cpu-memory"].GetProperty("properties").EnumerateArray().Select(value => value.GetString())));
        Assert.Equal(5, constraints["consumption-cpu-memory"].GetProperty("tuples").GetArrayLength());
        Assert.Equal("workloadProfileName", constraints["consumption-cpu-memory"].GetProperty("when").GetProperty("property").GetString());
        Assert.Equal("Consumption", constraints["consumption-cpu-memory"].GetProperty("when").GetProperty("equals").GetString());

        var knownAppProperties = appProperties.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var constraint in constraints.Values)
        {
            if (constraint.GetProperty("kind").GetString() == "compare")
            {
                Assert.Contains(constraint.GetProperty("left").GetString()!, knownAppProperties);
                Assert.Contains(constraint.GetProperty("right").GetString()!, knownAppProperties);
            }
            else
            {
                foreach (var property in constraint.GetProperty("properties").EnumerateArray())
                {
                    Assert.Contains(property.GetString()!, knownAppProperties);
                }
            }
        }
    }

    private static bool IsValidResourceDescriptor(JsonObject descriptor)
    {
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(CatalogRoot, "schema", "resource-type.schema.json")));
        using var candidate = JsonDocument.Parse(descriptor.ToJsonString());
        return schema.Evaluate(candidate.RootElement).IsValid
            && HasValidConstraintSemantics(candidate.RootElement);
    }

    private static bool HasValidConstraintSemantics(JsonElement descriptor)
    {
        var properties = descriptor.GetProperty("properties").EnumerateArray().ToArray();
        var propertyDefinitions = properties.ToDictionary(
            property => property.GetProperty("name").GetString()!,
            StringComparer.Ordinal);

        if (descriptor.TryGetProperty("constraints", out var constraints)
            && !AreValidConstraints(constraints, propertyDefinitions))
        {
            return false;
        }

        foreach (var property in properties)
        {
            if (!property.TryGetProperty("items", out var items)
                || !items.TryGetProperty("members", out var members)
                || !items.TryGetProperty("constraints", out var itemConstraints))
            {
                continue;
            }

            var memberDefinitions = members.EnumerateArray().ToDictionary(
                member => member.GetProperty("name").GetString()!,
                StringComparer.Ordinal);
            if (!AreValidConstraints(itemConstraints, memberDefinitions))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreValidConstraints(
        JsonElement constraints,
        Dictionary<string, JsonElement> propertyDefinitions)
    {
        var constraintIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var constraint in constraints.EnumerateArray())
        {
            if (!constraintIds.Add(constraint.GetProperty("id").GetString()!))
            {
                return false;
            }

            var kind = constraint.GetProperty("kind").GetString();
            if (string.Equals(kind, "compare", StringComparison.Ordinal))
            {
                if (!IsNumericProperty(constraint.GetProperty("left").GetString()!, propertyDefinitions)
                    || !IsNumericProperty(constraint.GetProperty("right").GetString()!, propertyDefinitions))
                {
                    return false;
                }

                continue;
            }

            if (!string.Equals(kind, "allowedTuples", StringComparison.Ordinal))
            {
                return false;
            }

            var tupleProperties = constraint.GetProperty("properties").EnumerateArray()
                .Select(property => property.GetString()!)
                .ToArray();
            if (tupleProperties.Any(property => !propertyDefinitions.ContainsKey(property)))
            {
                return false;
            }

            foreach (var tuple in constraint.GetProperty("tuples").EnumerateArray())
            {
                if (tuple.ValueKind != JsonValueKind.Array || tuple.GetArrayLength() != tupleProperties.Length)
                {
                    return false;
                }

                var values = tuple.EnumerateArray().ToArray();
                for (var index = 0; index < tupleProperties.Length; index++)
                {
                    if (CatalogPropertyValueValidator.Validate(values[index], propertyDefinitions[tupleProperties[index]]) is not null)
                    {
                        return false;
                    }
                }
            }

            if (constraint.TryGetProperty("when", out var condition))
            {
                var conditionProperty = condition.GetProperty("property").GetString()!;
                if (!propertyDefinitions.TryGetValue(conditionProperty, out var conditionDefinition)
                    || CatalogPropertyValueValidator.Validate(condition.GetProperty("equals"), conditionDefinition) is not null)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsNumericProperty(
        string propertyName,
        Dictionary<string, JsonElement> propertyDefinitions) =>
        propertyDefinitions.TryGetValue(propertyName, out var definition)
        && definition.GetProperty("kind").GetString() is "integer" or "number";

    [Fact]
    public void LogAnalyticsAccessIsEnabledByPatchWithoutMutatingPinnedPilot()
    {
        var provider = CatalogLoader.Load(typeof(CatalogLoader).Assembly);
        var original = provider.GetVersion("2026.10-pilot");
        var accessPatch = provider.GetVersion("2026.10-pilot.1");
        var exposurePatch = provider.GetVersion("2026.10-pilot.2");
        var latestPatch = provider.GetVersion("2026.10-pilot.3");
        Assert.NotNull(original);
        Assert.NotNull(accessPatch);
        Assert.NotNull(exposurePatch);
        Assert.NotNull(latestPatch);

        var originalTargetLinks = original.GetResourceType("LogAnalyticsWorkspace")!.Definition
            .GetProperty("links").GetProperty("asTarget").EnumerateArray();
        var patchTargetLinks = accessPatch.GetResourceType("LogAnalyticsWorkspace")!.Definition
            .GetProperty("links").GetProperty("asTarget").EnumerateArray();
        var exposurePatchTargetLinks = exposurePatch.GetResourceType("LogAnalyticsWorkspace")!.Definition
            .GetProperty("links").GetProperty("asTarget").EnumerateArray();
        var latestPatchTargetLinks = latestPatch.GetResourceType("LogAnalyticsWorkspace")!.Definition
            .GetProperty("links").GetProperty("asTarget").EnumerateArray();

        Assert.DoesNotContain(originalTargetLinks, kind => kind.GetString() == "Access");
        Assert.Contains(patchTargetLinks, kind => kind.GetString() == "Access");
        Assert.Contains(exposurePatchTargetLinks, kind => kind.GetString() == "Access");
        Assert.Contains(latestPatchTargetLinks, kind => kind.GetString() == "Access");
        Assert.Equal("2026.10-pilot.5", provider.LatestPublished?.Version);
    }

    [Fact]
    public void ExposurePropertiesArePublishedOnlyForTypesThatSupportThem()
    {
        var provider = CatalogLoader.Load(typeof(CatalogLoader).Assembly);
        var original = provider.GetVersion("2026.10-pilot")!;
        var accessPatch = provider.GetVersion("2026.10-pilot.1")!;
        var exposurePatch = provider.GetVersion("2026.10-pilot.2")!;
        var supportedTypes = new[] { "ContainerRegistry", "KeyVault", "SqlServer" };

        foreach (var type in supportedTypes)
        {
            var descriptor = exposurePatch.GetResourceType(type)!;
            var properties = descriptor.Definition.GetProperty("properties").EnumerateArray().ToArray();
            var exposure = properties.Single(property => property.GetProperty("name").GetString() == "exposure");
            var allowedRanges = properties.Single(property => property.GetProperty("name").GetString() == "allowedIpRanges");
            var modes = descriptor.Definition.GetProperty("exposure").GetProperty("modes")
                .EnumerateArray().Select(mode => mode.GetString()).ToArray();

            Assert.Equal("enum", exposure.GetProperty("kind").GetString());
            Assert.Equal("Public", exposure.GetProperty("default").GetString());
            Assert.True(exposure.GetProperty("overridable").GetBoolean());
            Assert.Equal(modes, exposure.GetProperty("values").EnumerateArray().Select(value => value.GetString()).ToArray());
            Assert.Equal("array", allowedRanges.GetProperty("kind").GetString());
            Assert.True(allowedRanges.GetProperty("overridable").GetBoolean());
            Assert.Equal(100, allowedRanges.GetProperty("maxItems").GetInt32());
            Assert.Equal("requires:exposure=Restricted", allowedRanges.GetProperty("condition").GetString());
            Assert.Equal("string", allowedRanges.GetProperty("items").GetProperty("kind").GetString());
            Assert.Equal("ipv4Cidr", allowedRanges.GetProperty("items").GetProperty("format").GetString());

            Assert.DoesNotContain(original.GetResourceType(type)!.Definition.GetProperty("properties").EnumerateArray(),
                property => property.GetProperty("name").GetString() == "exposure");
            Assert.DoesNotContain(accessPatch.GetResourceType(type)!.Definition.GetProperty("properties").EnumerateArray(),
                property => property.GetProperty("name").GetString() == "exposure");
        }

        foreach (var descriptor in exposurePatch.ResourceTypes.Where(resourceType => !supportedTypes.Contains(resourceType.Type)))
        {
            Assert.DoesNotContain(descriptor.Definition.GetProperty("properties").EnumerateArray(),
                property => property.GetProperty("name").GetString() is "exposure" or "allowedIpRanges");
        }
    }

    [Fact]
    public void SqlServerEntraAdministratorUsesTypedPropertiesAndAnExplicitBicepObjectMapping()
    {
        var provider = CatalogLoader.Load(typeof(CatalogLoader).Assembly);
        var latest = provider.LatestPublished;

        Assert.Equal("2026.10-pilot.5", latest?.Version);

        var descriptor = latest!.GetResourceType("SqlServer")!.Definition;
        var properties = descriptor.GetProperty("properties").EnumerateArray()
            .ToDictionary(property => property.GetProperty("name").GetString()!, StringComparer.Ordinal);
        var displayName = properties["administratorDisplayName"];
        var objectId = properties["administratorObjectId"];
        var administratorLogin = properties["administratorLogin"];

        Assert.Equal("string", displayName.GetProperty("kind").GetString());
        Assert.Equal("requiredWhen:authentication", displayName.GetProperty("condition").GetString());
        Assert.Equal("string", objectId.GetProperty("kind").GetString());
        Assert.Equal("requiredWhen:authentication", objectId.GetProperty("condition").GetString());
        Assert.Matches(objectId.GetProperty("pattern").GetString()!, "00000000-0000-0000-0000-000000000001");
        Assert.Equal(1, administratorLogin.GetProperty("minLength").GetInt32());
        Assert.Equal(128, administratorLogin.GetProperty("maxLength").GetInt32());
        Assert.Equal("requiredWhen:authentication=EntraEtSql", administratorLogin.GetProperty("condition").GetString());
        var administratorLoginPattern = administratorLogin.GetProperty("pattern").GetString()!;
        Assert.DoesNotMatch(administratorLoginPattern, "administrator");
        Assert.DoesNotMatch(administratorLoginPattern, "ADMINISTRATOR");
        Assert.Matches(administratorLoginPattern, "sqladmin");
        Assert.DoesNotMatch(administratorLoginPattern, "sqladmin\n");

        var bicep = descriptor.GetProperty("generation").GetProperty("Bicep");
        Assert.Equal("administratorLogin", bicep.GetProperty("map").GetProperty("administratorLogin").GetString());
        var administrators = bicep.GetProperty("parameterObjects").GetProperty("administrators");
        Assert.Equal("administratorDisplayName", administrators.GetProperty("login").GetProperty("ref").GetString());
        Assert.Equal("administratorObjectId", administrators.GetProperty("sid").GetProperty("ref").GetString());
        Assert.Equal("Group", administrators.GetProperty("principalType").GetProperty("const").GetString());

        var authentication = administrators.GetProperty("azureADOnlyAuthentication");
        Assert.Equal("authentication", authentication.GetProperty("enumRef").GetString());
        Assert.True(authentication.GetProperty("values").GetProperty("EntraSeule").GetBoolean());
        Assert.False(authentication.GetProperty("values").GetProperty("EntraEtSql").GetBoolean());
    }

    [Fact]
    public void LatestPublishedConditionsReferenceDeclaredProperties()
    {
        var latest = CatalogLoader.Load(typeof(CatalogLoader).Assembly).LatestPublished;
        Assert.NotNull(latest);

        foreach (var descriptor in latest.ResourceTypes)
        {
            var properties = descriptor.Definition.GetProperty("properties").EnumerateArray().ToArray();
            var propertyNames = properties
                .Select(property => property.GetProperty("name").GetString()!)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var property in properties.Where(property => property.TryGetProperty("condition", out _)))
            {
                var condition = property.GetProperty("condition").GetString()!;
                var requirement = condition[(condition.IndexOf(':') + 1)..];
                var dependency = requirement.Split('=', 2)[0];

                Assert.Contains(dependency, propertyNames);
            }
        }
    }

    [Fact]
    public void CatalogDoesNotClaimDirectBicepMappingsForUnsupportedConversions()
    {
        var provider = CatalogLoader.Load(typeof(CatalogLoader).Assembly);

        AssertMapDoesNotContain(provider, "ApplicationInsights", "samplingPercentage");
        AssertMapDoesNotContain(provider, "ContainerApp", "ingress");
        AssertMapDoesNotContain(provider, "ContainerApp", "maxReplicas");
        AssertMapDoesNotContain(provider, "ContainerRegistry", "zoneRedundancy");
        AssertMapDoesNotContain(provider, "LogAnalyticsWorkspace", "dailyQuotaGb");
        AssertMapDoesNotContain(provider, "SqlDatabase", "database");
        AssertMapDoesNotContain(provider, "SqlServer", "publicNetworkAccess");
    }

    private static void AssertMapDoesNotContain(ICatalogProvider provider, string resourceType, string propertyName)
    {
        var descriptor = provider.LatestPublished!.GetResourceType(resourceType)!.Definition;
        var mappings = descriptor.GetProperty("generation").GetProperty("Bicep").GetProperty("map");

        Assert.False(mappings.TryGetProperty(propertyName, out _),
            $"{resourceType}.{propertyName} needs a generation transformation that the current map format cannot express.");
    }

    [Fact]
    public void PublishedPilotVersionsContainNineResourceDescriptors()
    {
        var provider = CatalogLoader.Load(typeof(CatalogLoader).Assembly);

        Assert.Equal(9, provider.GetVersion("2026.10-pilot")!.ResourceTypes.Length);
        Assert.Equal(9, provider.GetVersion("2026.10-pilot.1")!.ResourceTypes.Length);
        Assert.Equal(9, provider.GetVersion("2026.10-pilot.2")!.ResourceTypes.Length);
        Assert.Equal(9, provider.GetVersion("2026.10-pilot.3")!.ResourceTypes.Length);
    }

    [Fact]
    public void ContainerBuildProfileIsAddedAsANewPublishedPatch()
    {
        var provider = CatalogLoader.Load(typeof(CatalogLoader).Assembly);
        var containerCatalog = provider.GetVersion("2026.10-pilot.3");
        var latestCatalog = provider.GetVersion("2026.10-pilot.4");

        Assert.NotNull(containerCatalog);
        Assert.NotNull(latestCatalog);
        Assert.Equal("2026.10-pilot.5", provider.LatestPublished?.Version);

        var buildProfilePath = Path.Combine(CatalogRoot, "2026.10-pilot.3", "build-profiles", "Container.json");
        Assert.True(File.Exists(buildProfilePath), "The Container profile must be published in its own catalog patch.");

        using var profile = JsonDocument.Parse(File.ReadAllText(buildProfilePath));
        Assert.Equal("Container", profile.RootElement.GetProperty("name").GetString());
        Assert.Equal("2026.10-pilot.2", provider.GetVersion("2026.10-pilot.2")?.Version);

        Assert.Empty(provider.GetVersion("2026.10-pilot.2")!.BuildProfiles);
        Assert.Equal("ContainerApp", containerCatalog.BuildProfiles.Single().ResourceTypes.Single());
        Assert.Collection(
            containerCatalog.BuildProfiles.Single().Steps,
            step => Assert.Equal("DependencyCache", step),
            step => Assert.Equal("ImageScan", step));
        Assert.Equal("Container", latestCatalog.BuildProfiles.Single().Name);
        Assert.Equal(containerCatalog.BuildProfiles.Single().ResourceTypes.ToArray(), latestCatalog.BuildProfiles.Single().ResourceTypes.ToArray());
        Assert.Equal(containerCatalog.BuildProfiles.Single().Steps.ToArray(), latestCatalog.BuildProfiles.Single().Steps.ToArray());
        Assert.Contains(containerCatalog.PipelineSteps, step => step.Name == "DependencyCache" && step.DefaultEnabled);
        var imageScan = Assert.Single(containerCatalog.PipelineSteps.Where(step => step.Name == "ImageScan"));
        Assert.True(imageScan.DefaultBlocking);
        Assert.Equal("CRITICAL", imageScan.Parameters.Single(parameter => parameter.Name == "minimumSeverity").DefaultValue);
    }

    [Fact]
    public void EveryCatalogDocumentSatisfiesItsPublishedSchema()
    {
        foreach (var versionDirectory in CatalogVersionDirectories())
        {
            var version = Path.GetFileName(versionDirectory);
            AssertValid("schema/catalog-version.schema.json", $"{version}/catalog.json");
            AssertValid("schema/regions.schema.json", $"{version}/regions.json");
            AssertValid("schema/role-catalog.schema.json", $"{version}/roles.json");

            foreach (var descriptorPath in Directory.GetFiles(Path.Combine(versionDirectory, "types"), "*.json"))
            {
                AssertValid("schema/resource-type.schema.json", Path.GetRelativePath(CatalogRoot, descriptorPath));
            }

            foreach (var stepPath in Directory.GetFiles(Path.Combine(versionDirectory, "steps"), "*.json"))
            {
                AssertValid("schema/step.schema.json", Path.GetRelativePath(CatalogRoot, stepPath));
            }

            var buildProfilesDirectory = Path.Combine(versionDirectory, "build-profiles");
            if (Directory.Exists(buildProfilesDirectory))
            {
                foreach (var profilePath in Directory.GetFiles(buildProfilesDirectory, "*.json"))
                {
                    AssertValid("schema/build-profile.schema.json", Path.GetRelativePath(CatalogRoot, profilePath));
                }
            }
        }
    }

    [Fact]
    public void EveryPublishedCatalogReferenceTargetsADeclaredPropertyOrMember()
    {
        foreach (var versionDirectory in CatalogVersionDirectories())
        {
            var documents = Directory.GetFiles(Path.Combine(versionDirectory, "types"), "*.json")
                .Select(path => JsonDocument.Parse(File.ReadAllText(path)))
                .ToArray();
            try
            {
                foreach (var descriptor in documents)
                {
                    var root = descriptor.RootElement;
                    var resourceType = root.GetProperty("type").GetString()!;
                    Assert.True(HasValidConstraintSemantics(root),
                        $"{resourceType} contains a constraint incompatible with its declared property types or values.");
                    var properties = root.GetProperty("properties");
                    var propertyNames = properties.EnumerateArray()
                        .Select(property => property.GetProperty("name").GetString()!)
                        .ToHashSet(StringComparer.Ordinal);

                    if (root.TryGetProperty("constraints", out var constraints))
                    {
                        AssertConstraintReferencesExist(constraints, propertyNames, resourceType);
                    }

                    AssertPropertyReferencesExist(properties, resourceType, documents);
                    if (root.TryGetProperty("children", out var children))
                    {
                        foreach (var child in children.EnumerateArray())
                        {
                            var childType = child.GetProperty("type").GetString()!;
                            var childProperties = child.GetProperty("properties");
                            var childPropertyNames = childProperties.EnumerateArray()
                                .Select(property => property.GetProperty("name").GetString()!)
                                .ToHashSet(StringComparer.Ordinal);
                            if (child.TryGetProperty("constraints", out var childConstraints))
                            {
                                AssertConstraintReferencesExist(childConstraints, childPropertyNames, $"{resourceType}.{childType}");
                            }

                            AssertPropertyReferencesExist(childProperties, $"{resourceType}.{childType}", documents);
                        }
                    }
                }
            }
            finally
            {
                foreach (var document in documents)
                {
                    document.Dispose();
                }
            }
        }
    }

    [Fact]
    public void ResourcePropertySchemaAllowsAnOptionalPattern()
    {
        var path = Path.Combine(PilotRoot, "types", "SqlServer.json");
        var descriptor = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var collation = descriptor["children"]![0]!["properties"]!.AsArray()
            .Single(property => property!["name"]!.GetValue<string>() == "collation")!
            .AsObject();
        collation["pattern"] = "^[A-Za-z0-9_]+$";
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(CatalogRoot, "schema", "resource-type.schema.json")));
        using var candidate = JsonDocument.Parse(descriptor.ToJsonString());

        Assert.True(schema.Evaluate(candidate.RootElement).IsValid);
    }

    [Fact]
    public void EveryPublishedVersionMatchesItsPinnedFingerprint()
    {
        var provider = CatalogLoader.Load(typeof(CatalogLoader).Assembly);
        using var hashes = JsonDocument.Parse(File.ReadAllText(Path.Combine(CatalogRoot, "published-hashes.json")));

        foreach (var version in provider.GetVersions().Where(candidate => candidate.Status == "published"))
        {
            Assert.True(
                hashes.RootElement.TryGetProperty(version.Version, out var expected),
                $"Add {version.Version} to published-hashes.json with fingerprint {version.Fingerprint}.");
            Assert.Equal(expected.GetString(), version.Fingerprint);
        }
    }

    [Fact]
    public void EveryResourceDescriptorRoleExistsInThePinnedRoleCatalog()
    {
        foreach (var versionDirectory in CatalogVersionDirectories())
        {
            using var rolesDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(versionDirectory, "roles.json")));
            var roleNames = rolesDocument.RootElement.GetProperty("roles")
                .EnumerateArray()
                .Select(role => role.GetProperty("name").GetString())
                .ToHashSet(StringComparer.Ordinal);

            foreach (var path in Directory.GetFiles(Path.Combine(versionDirectory, "types"), "*.json"))
            {
                using var descriptor = JsonDocument.Parse(File.ReadAllText(path));
                var directRoles = descriptor.RootElement.GetProperty("roles").EnumerateArray()
                    .Select(role => role.GetString()!);
                var equivalentRoles = descriptor.RootElement.GetProperty("roleEquivalences")
                    .EnumerateObject()
                    .SelectMany(group => group.Value.EnumerateArray())
                    .Select(role => role.GetString()!);
                foreach (var role in directRoles.Concat(equivalentRoles))
                {
                    Assert.Contains(role, roleNames);
                }
            }
        }
    }

    [Fact]
    public void EveryBicepMappingMatchesAParameterInItsRestoredAvmModule()
    {
        var bicepCacheRoot = Environment.GetEnvironmentVariable("BICEP_CACHE_ROOT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bicep", "br", "mcr.microsoft.com");

        foreach (var versionDirectory in CatalogVersionDirectories())
        {
            using var pins = JsonDocument.Parse(File.ReadAllText(Path.Combine(versionDirectory, "pins.json")));
            var modules = pins.RootElement.GetProperty("modules");

            foreach (var descriptorPath in Directory.GetFiles(Path.Combine(versionDirectory, "types"), "*.json"))
            {
                using var descriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));
                var generation = descriptor.RootElement.GetProperty("generation").GetProperty("Bicep");
                var moduleReference = generation.GetProperty("module").GetString()!;
                var versionPin = generation.GetProperty("versionPin").GetString()!;
                var module = modules.EnumerateObject()
                    .Select(property => property.Value)
                    .Single(pin => pin.GetProperty("reference").GetString() == moduleReference);
                var pinnedVersion = module.GetProperty("version").GetString()!;
                Assert.Equal($"{pinnedVersion}.0", versionPin);

                var relativeModulePath = moduleReference
                    .Replace("br/public:", string.Empty, StringComparison.Ordinal)
                    .Split(':', 2)[0]
                    .Replace('/', '$');
                var cacheModuleName = "bicep$" + relativeModulePath;
                var moduleJson = Path.Combine(bicepCacheRoot, cacheModuleName, versionPin + "$", "main.json");
                Assert.True(File.Exists(moduleJson), $"Restore the pinned module before testing: {moduleJson}");

                using var moduleDocument = JsonDocument.Parse(File.ReadAllText(moduleJson));
                var parameterNames = moduleDocument.RootElement.GetProperty("parameters")
                    .EnumerateObject()
                    .Select(parameter => parameter.Name)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var mapping in generation.GetProperty("map").EnumerateObject())
                {
                    Assert.Contains(mapping.Value.GetString()!, parameterNames);
                }

                if (generation.TryGetProperty("parameterObjects", out var parameterObjects))
                {
                    var mappedParameters = generation.GetProperty("map").EnumerateObject()
                        .Select(mapping => mapping.Value.GetString()!)
                        .ToHashSet(StringComparer.Ordinal);
                    var descriptorProperties = descriptor.RootElement.GetProperty("properties").EnumerateArray()
                        .ToDictionary(property => property.GetProperty("name").GetString()!, StringComparer.Ordinal);
                    var moduleRoot = moduleDocument.RootElement;
                    var moduleParameters = moduleRoot.GetProperty("parameters");

                    foreach (var objectMapping in parameterObjects.EnumerateObject())
                    {
                        Assert.DoesNotContain(objectMapping.Name, mappedParameters);
                        Assert.True(parameterNames.Contains(objectMapping.Name),
                            $"{descriptorPath}: parameterObjects entry '{objectMapping.Name}' is not an AVM parameter.");

                        var targetMembers = ResolveObjectParameterMembers(moduleRoot, moduleParameters.GetProperty(objectMapping.Name));
                        foreach (var memberMapping in objectMapping.Value.EnumerateObject())
                        {
                            Assert.True(targetMembers.TryGetProperty(memberMapping.Name, out var targetMember),
                                $"{descriptorPath}: parameter '{objectMapping.Name}' has no member '{memberMapping.Name}'.");
                            ValidateObjectMemberMapping(
                                descriptorPath,
                                objectMapping.Name,
                                memberMapping.Name,
                                memberMapping.Value,
                                targetMember,
                                descriptorProperties);
                        }
                    }
                }
            }
        }
    }

    private static JsonElement ResolveObjectParameterMembers(JsonElement moduleRoot, JsonElement parameter)
    {
        var hasReference = parameter.TryGetProperty("$ref", out var reference);
        Assert.True(hasReference && reference.ValueKind == JsonValueKind.String,
            "An AVM object parameter must have a string $ref type.");
        const string referencePrefix = "#/definitions/";
        var referenceName = reference.GetString()!;
        Assert.StartsWith(referencePrefix, referenceName, StringComparison.Ordinal);
        var definitionName = referenceName[referencePrefix.Length..];
        Assert.True(moduleRoot.TryGetProperty("definitions", out var definitions), "The AVM module must declare definitions.");
        Assert.True(definitions.TryGetProperty(definitionName, out var definition),
            $"The AVM object definition '{definitionName}' must exist.");
        Assert.True(definition.TryGetProperty("properties", out var properties)
            && properties.ValueKind == JsonValueKind.Object,
            $"The AVM object definition '{definitionName}' must declare inline properties.");
        return properties;
    }

    private static void ValidateObjectMemberMapping(
        string descriptorPath,
        string parameterName,
        string memberName,
        JsonElement mapping,
        JsonElement targetMember,
        Dictionary<string, JsonElement> descriptorProperties)
    {
        if (mapping.TryGetProperty("ref", out var reference))
        {
            var sourceName = reference.GetString();
            Assert.NotNull(sourceName);
            Assert.True(descriptorProperties.TryGetValue(sourceName, out var source),
                $"{descriptorPath}: mapping for '{parameterName}.{memberName}' references an unknown resource property.");
            AssertMatchesBicepDescriptorType(
                descriptorPath,
                parameterName,
                memberName,
                source.GetProperty("kind").GetString()!,
                ResolveTargetMemberType(targetMember, descriptorPath, parameterName, memberName));
            return;
        }

        if (mapping.TryGetProperty("const", out var constant))
        {
            AssertMatchesBicepValueType(
                descriptorPath,
                parameterName,
                memberName,
                constant.ValueKind,
                ResolveTargetMemberType(targetMember, descriptorPath, parameterName, memberName));
            AssertAllowedValue(targetMember, constant, descriptorPath, parameterName, memberName);
            return;
        }

        var enumName = mapping.GetProperty("enumRef").GetString()!;
        var enumDefinition = descriptorProperties[enumName];
        Assert.Equal("enum", enumDefinition.GetProperty("kind").GetString());
        var expectedEnumValues = enumDefinition.GetProperty("values").EnumerateArray()
            .Select(value => value.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var values = mapping.GetProperty("values");
        Assert.True(expectedEnumValues.SetEquals(values.EnumerateObject().Select(value => value.Name)));

        foreach (var value in values.EnumerateObject())
        {
            AssertMatchesBicepValueType(
                descriptorPath,
                parameterName,
                memberName,
                value.Value.ValueKind,
                ResolveTargetMemberType(targetMember, descriptorPath, parameterName, memberName));
            AssertAllowedValue(targetMember, value.Value, descriptorPath, parameterName, memberName);
        }
    }

    private static string ResolveTargetMemberType(
        JsonElement targetMember,
        string descriptorPath,
        string parameterName,
        string memberName)
    {
        var hasType = targetMember.TryGetProperty("type", out var type);
        Assert.True(hasType && type.ValueKind == JsonValueKind.String,
            $"{descriptorPath}: AVM parameter '{parameterName}.{memberName}' must declare an inline string type.");
        return type.GetString()!;
    }

    private static void AssertMatchesBicepDescriptorType(
        string descriptorPath,
        string parameterName,
        string memberName,
        string descriptorKind,
        string targetType)
    {
        var matches = (descriptorKind, targetType) switch
        {
            ("string" or "enum" or "reference", "string") => true,
            ("boolean", "bool") => true,
            ("integer" or "number", "int" or "int32" or "int64" or "number") => true,
            _ => false
        };
        Assert.True(matches,
            $"{descriptorPath}: mapping for '{parameterName}.{memberName}' has an incompatible source type for AVM type '{targetType}'.");
    }

    private static void AssertMatchesBicepValueType(
        string descriptorPath,
        string parameterName,
        string memberName,
        JsonValueKind valueKind,
        string targetType)
    {
        var matches = (valueKind, targetType) switch
        {
            (JsonValueKind.String, "string") => true,
            (JsonValueKind.True or JsonValueKind.False, "bool") => true,
            (JsonValueKind.Number, "int" or "int32" or "int64" or "number") => true,
            _ => false
        };
        Assert.True(matches,
            $"{descriptorPath}: mapping for '{parameterName}.{memberName}' has an incompatible source type for AVM type '{targetType}'.");
    }

    private static void AssertAllowedValue(
        JsonElement targetMember,
        JsonElement value,
        string descriptorPath,
        string parameterName,
        string memberName)
    {
        if (!targetMember.TryGetProperty("allowedValues", out var allowedValues))
        {
            return;
        }

        var isAllowed = allowedValues.EnumerateArray().Any(candidate => JsonElement.DeepEquals(candidate, value));
        Assert.True(isAllowed,
            $"{descriptorPath}: mapping for '{parameterName}.{memberName}' uses a value outside the AVM module's allowed values.");
    }

    [Fact]
    public void RegionsAndPinnedAzurePipelinesSchemaMatchTheirSourceData()
    {
        foreach (var versionDirectory in CatalogVersionDirectories())
        {
            using var regions = JsonDocument.Parse(File.ReadAllText(Path.Combine(versionDirectory, "regions.json")));
            var regionItems = regions.RootElement.GetProperty("regions").EnumerateArray().ToArray();
            Assert.Equal(24, regionItems.Length);
            Assert.Equal(24, regionItems.Select(region => region.GetProperty("name").GetString()).Distinct().Count());
            Assert.Equal(24, regionItems.Select(region => region.GetProperty("code").GetString()).Distinct().Count());

            using var pins = JsonDocument.Parse(File.ReadAllText(Path.Combine(versionDirectory, "pins.json")));
            var expectedHash = pins.RootElement
                .GetProperty("pipelineValidation")
                .GetProperty("azurePipelinesSchema")
                .GetProperty("sha256")
                .GetString()!;
            var actualHash = Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(Path.Combine(versionDirectory, "schemas", "azure-pipelines.json"))))
                .ToLowerInvariant();

            Assert.Equal(expectedHash, actualHash);
        }
    }

    private static void AssertConstraintReferencesExist(
        JsonElement constraints,
        IReadOnlySet<string> declaredProperties,
        string context)
    {
        foreach (var constraint in constraints.EnumerateArray())
        {
            var references = new List<string>();
            foreach (var propertyName in new[] { "left", "right" })
            {
                if (constraint.TryGetProperty(propertyName, out var property))
                {
                    references.Add(property.GetString()!);
                }
            }

            if (constraint.TryGetProperty("properties", out var properties))
            {
                references.AddRange(properties.EnumerateArray().Select(property => property.GetString()!));
            }

            if (constraint.TryGetProperty("when", out var condition)
                && condition.TryGetProperty("property", out var conditionProperty))
            {
                references.Add(conditionProperty.GetString()!);
            }

            foreach (var reference in references)
            {
                Assert.Contains(reference, declaredProperties);
            }
        }
    }

    private static void AssertPropertyReferencesExist(
        JsonElement properties,
        string context,
        IReadOnlyList<JsonDocument> resourceDescriptors)
    {
        foreach (var property in properties.EnumerateArray())
        {
            var propertyName = property.GetProperty("name").GetString()!;
            if (property.TryGetProperty("items", out var items))
            {
                var memberNames = items.TryGetProperty("members", out var members)
                    ? members.EnumerateArray()
                        .Select(member => member.GetProperty("name").GetString()!)
                        .ToHashSet(StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);

                if (items.TryGetProperty("uniqueBy", out var uniqueBy))
                {
                    Assert.Contains(uniqueBy.GetString()!, memberNames);
                }

                if (items.TryGetProperty("constraints", out var itemConstraints))
                {
                    AssertConstraintReferencesExist(itemConstraints, memberNames, $"{context}.{propertyName} items");
                }
            }

            if (!property.TryGetProperty("allowedFrom", out var allowedFrom))
            {
                continue;
            }

            var linkKind = allowedFrom.GetProperty("linkKind").GetString()!;
            var targetProperty = allowedFrom.GetProperty("targetProperty").GetString()!;
            var targetMember = allowedFrom.GetProperty("targetMember").GetString()!;
            var linkedTargetDeclaresMember = resourceDescriptors.Any(targetDescriptor =>
            {
                var target = targetDescriptor.RootElement;
                if (!target.TryGetProperty("links", out var links)
                    || !links.TryGetProperty("asTarget", out var linkKinds)
                    || !linkKinds.EnumerateArray().Any(kind => kind.GetString() == linkKind)
                    || !target.TryGetProperty("properties", out var targetProperties))
                {
                    return false;
                }

                var targetPropertyDescriptor = targetProperties.EnumerateArray()
                    .FirstOrDefault(candidate => candidate.GetProperty("name").GetString() == targetProperty);
                return targetPropertyDescriptor.ValueKind == JsonValueKind.Object
                    && targetPropertyDescriptor.TryGetProperty("items", out var targetItems)
                    && targetItems.TryGetProperty("members", out var targetMembers)
                    && targetMembers.EnumerateArray().Any(member => member.GetProperty("name").GetString() == targetMember);
            });

            Assert.True(
                linkedTargetDeclaresMember,
                $"{context}.{propertyName} references undeclared linked target member {linkKind}:{targetProperty}.{targetMember}.");
        }
    }

    private static void AssertValid(string schemaRelativePath, string documentRelativePath)
    {
        var schemaPath = Path.Combine(CatalogRoot, schemaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var documentPath = Path.Combine(CatalogRoot, documentRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(schemaPath), $"Schema is missing: {schemaPath}");
        Assert.True(File.Exists(documentPath), $"Catalog document is missing: {documentPath}");

        var schema = JsonSchema.FromText(File.ReadAllText(schemaPath));
        using var document = JsonDocument.Parse(File.ReadAllText(documentPath));
        var result = schema.Evaluate(document.RootElement);
        Assert.True(result.IsValid, $"{documentRelativePath}: {result}");
    }
}
