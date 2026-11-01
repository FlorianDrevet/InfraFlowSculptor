using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.Bicep;

internal static class BicepCatalogSupportValidator
{
    public static void Validate(DeploymentPlan plan, CatalogVersion catalog)
    {
        foreach (var component in plan.Components)
        {
            ValidateExternalReferenceParameterPaths(component, catalog);
            foreach (var target in component.Targets)
            {
                ValidateNoExistingLocalResources(component, target);
                ValidateResourceMultiplicity(component, target);

                foreach (var resource in target.Resources.Where(resource => !resource.IsExisting))
                {
                    var descriptor = catalog.GetResourceType(resource.Type)
                        ?? throw new InvalidOperationException(
                            $"The pinned catalog has no descriptor for resource type '{resource.Type}'.");
                    ValidateUnsupportedValues(resource, descriptor);
                    ValidateExposureSupport(resource, descriptor);
                    ValidateRequirements(resource, descriptor);
                }

                ValidateRequiredTargetShapes(component, target, catalog);
                ValidateApplicationSettingSupport(component, target, catalog);
            }

            ValidateDiagnosticDestinations(component, catalog);
        }
    }

    private static void ValidateNoExistingLocalResources(ComponentPlan component, TargetPlan target)
    {
        var existing = target.Resources.FirstOrDefault(resource => resource.IsExisting);
        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"Bicep generation does not support existing local resource '{existing.Type}/{existing.LogicalName}' "
                + $"in component '{component.ComponentCode}' target '{target.Target.Code}'; model it as an external reference.");
        }
    }

    private static void ValidateRequiredTargetShapes(ComponentPlan component, TargetPlan target, CatalogVersion catalog)
    {
        var localTypes = target.Resources.Select(resource => resource.Type).ToHashSet(StringComparer.Ordinal);
        var externalTypes = target.ExternalReferences.Select(reference => reference.ResourceType)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var resource in target.Resources.Where(resource => !resource.IsExisting))
        {
            var descriptor = catalog.GetResourceType(resource.Type)
                ?? throw new InvalidOperationException(
                    $"The pinned catalog has no descriptor for resource type '{resource.Type}'.");
            var bicep = BicepCatalogMetadata.GetBicep(descriptor);
            var missingResourceGroups = ReadStringArray(bicep, "requiredResourceGroups")
                .Where(requiredName => !target.ResourceGroups.Any(group =>
                    string.Equals(group.LogicalName, requiredName, StringComparison.Ordinal)))
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (missingResourceGroups.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Bicep generation for '{resource.Type}' in component '{component.ComponentCode}' "
                    + $"target '{target.Target.Code}' requires resource group logical names "
                    + $"[{string.Join(", ", missingResourceGroups)}] declared by the pinned catalog.");
            }

            if (!bicep.TryGetProperty("requiredTargetShape", out var shape))
            {
                continue;
            }

            var missingLocalTypes = ReadStringArray(shape, "localResourceTypes")
                .Where(type => !localTypes.Contains(type))
                .Order(StringComparer.Ordinal)
                .ToArray();
            var missingExternalTypes = ReadStringArray(shape, "externalResourceTypes")
                .Where(type => !externalTypes.Contains(type))
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (missingLocalTypes.Length == 0 && missingExternalTypes.Length == 0)
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Bicep generation for '{resource.Type}' in component '{component.ComponentCode}' "
                + $"target '{target.Target.Code}' requires local resource types [{string.Join(", ", missingLocalTypes)}] "
                + $"and external resource types [{string.Join(", ", missingExternalTypes)}]. "
                + BicepCatalogMetadata.ReadString(shape, "reason"));
        }
    }

    private static void ValidateApplicationSettingSupport(
        ComponentPlan component,
        TargetPlan target,
        CatalogVersion catalog)
    {
        foreach (var resource in target.Resources.Where(resource => !resource.IsExisting))
        {
            var descriptor = catalog.GetResourceType(resource.Type)
                ?? throw new InvalidOperationException(
                    $"The pinned catalog has no descriptor for resource type '{resource.Type}'.");
            var bicep = BicepCatalogMetadata.GetBicep(descriptor);
            if (!bicep.TryGetProperty("applicationSettingSupport", out var support))
            {
                continue;
            }

            var repeatedName = target.AppSettings
                .GroupBy(setting => setting.Name, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (repeatedName is not null)
            {
                throw new InvalidOperationException(
                    $"Bicep generation supports one application setting named '{repeatedName.Key}' "
                    + $"per component '{component.ComponentCode}' target '{target.Target.Code}'.");
            }

            var allowedNames = ReadStringArray(support, "allowedNames").ToHashSet(StringComparer.Ordinal);
            var unsupported = target.AppSettings.FirstOrDefault(setting => !allowedNames.Contains(setting.Name));
            if (unsupported is not null)
            {
                throw new InvalidOperationException(
                    $"Bicep generation does not support application setting '{unsupported.Name}' "
                    + $"for '{resource.Type}'. " + BicepCatalogMetadata.ReadString(support, "reason"));
            }

            if (!resource.Present)
            {
                continue;
            }

            var settingsByName = target.AppSettings.ToDictionary(setting => setting.Name, StringComparer.Ordinal);
            foreach (var requiredName in ReadStringArray(support, "requiredWhenPresent"))
            {
                if (!settingsByName.ContainsKey(requiredName))
                {
                    throw new InvalidOperationException(
                        $"Bicep generation for present '{resource.Type}' requires application setting '{requiredName}'.");
                }
            }

            foreach (var secretName in ReadStringArray(support, "requiredSecretNames"))
            {
                if (settingsByName.TryGetValue(secretName, out var setting)
                    && (string.IsNullOrWhiteSpace(setting.SecretName) || !setting.IsSensitive))
                {
                    throw new InvalidOperationException(
                        $"Bicep generation requires '{secretName}' to reference a sensitive Key Vault secret name.");
                }
            }
        }
    }

    private static string[] ReadStringArray(JsonElement definition, string propertyName) =>
        definition.TryGetProperty(propertyName, out var values)
            ? values.EnumerateArray().Select(value => value.GetString()!).ToArray()
            : [];

    private static void ValidateExternalReferenceParameterPaths(ComponentPlan component, CatalogVersion catalog)
    {
        var references = component.Targets.SelectMany(target => target.ExternalReferences)
            .Select(reference =>
            {
                var descriptor = catalog.GetResourceType(reference.ResourceType)
                    ?? throw new InvalidOperationException(
                        $"The pinned catalog has no descriptor for external resource type '{reference.ResourceType}'.");
                var existing = BicepCatalogMetadata.TryGetExisting(descriptor)
                    ?? throw new InvalidOperationException(
                        $"Resource type '{descriptor.Type}' has no Bicep existing-resource metadata.");
                var parameterPath = BicepCatalogMetadata.ReadString(existing, "parameterName")
                    ?? throw new InvalidOperationException(
                        $"Resource type '{descriptor.Type}' has no Bicep external parameter path.");
                var identity = $"{reference.ComponentCode}/{reference.ResourceType}/{reference.LogicalName}";
                return (ParameterPath: parameterPath, Identity: identity);
            })
            .GroupBy(reference => reference.ParameterPath, StringComparer.Ordinal)
            .Select(group => (ParameterPath: group.Key, Identities: group.Select(item => item.Identity)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()))
            .FirstOrDefault(group => group.Identities.Length > 1);

        if (references.ParameterPath is not null)
        {
            throw new InvalidOperationException(
                $"Multiple external resources map to Bicep parameter path '{references.ParameterPath}' "
                + $"in component '{component.ComponentCode}': {string.Join(", ", references.Identities)}.");
        }
    }

    private static void ValidateDiagnosticDestinations(ComponentPlan component, CatalogVersion catalog)
    {
        foreach (var setting in component.Targets.SelectMany(target => target.DiagnosticSettings))
        {
            var descriptor = catalog.GetResourceType(setting.SourceResourceType)
                ?? throw new InvalidOperationException(
                    $"The pinned catalog has no descriptor for diagnostic source '{setting.SourceResourceType}'.");
            var bicep = BicepCatalogMetadata.GetBicep(descriptor);
            if (!bicep.TryGetProperty("diagnosticDestination", out var expected))
            {
                throw new InvalidOperationException(
                    $"Bicep catalog metadata does not declare a diagnostic destination for '{setting.SourceResourceType}'.");
            }

            if (!string.Equals(setting.Destination.ComponentCode,
                    BicepCatalogMetadata.ReadString(expected, "componentCode"), StringComparison.Ordinal)
                || !string.Equals(setting.Destination.ResourceType,
                    BicepCatalogMetadata.ReadString(expected, "resourceType"), StringComparison.Ordinal)
                || !string.Equals(setting.Destination.LogicalName,
                    BicepCatalogMetadata.ReadString(expected, "logicalName"), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Bicep generation only supports the diagnostic destination declared by the catalog for "
                    + $"'{setting.SourceResourceType}': {BicepCatalogMetadata.ReadString(expected, "componentCode")}/"
                    + $"{BicepCatalogMetadata.ReadString(expected, "resourceType")}/"
                    + $"{BicepCatalogMetadata.ReadString(expected, "logicalName")}."
                );
            }
        }
    }

    private static void ValidateResourceMultiplicity(ComponentPlan component, TargetPlan target)
    {
        var repeatedType = target.Resources
            .Where(resource => !resource.IsExisting)
            .GroupBy(resource => resource.Type, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (repeatedType is not null)
        {
            var logicalNames = repeatedType.Select(resource => resource.LogicalName)
                .Order(StringComparer.Ordinal);
            throw new InvalidOperationException(
                $"Bicep catalog metadata supports one local resource per type in component '{component.ComponentCode}' "
                + $"target '{target.Target.Code}'; '{repeatedType.Key}' has: {string.Join(", ", logicalNames)}.");
        }
    }

    private static void ValidateUnsupportedValues(ResourcePlan resource, CatalogResourceTypeDescriptor descriptor)
    {
        var bicep = BicepCatalogMetadata.GetBicep(descriptor);
        if (!bicep.TryGetProperty("unsupportedValues", out var unsupportedValues))
        {
            return;
        }

        foreach (var unsupported in unsupportedValues.EnumerateArray())
        {
            var propertyName = unsupported.GetProperty("property").GetString()!;
            var strategy = unsupported.GetProperty("strategy").GetString();
            if (!string.Equals(strategy, "mustMatchDefault", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unsupported Bicep catalog strategy '{strategy}' for '{descriptor.Type}.{propertyName}'.");
            }

            var propertyDescriptor = descriptor.Definition.GetProperty("properties").EnumerateArray()
                .Single(property => string.Equals(
                    BicepCatalogMetadata.ReadString(property, "name"), propertyName, StringComparison.Ordinal));
            if (!propertyDescriptor.TryGetProperty("default", out var defaultValue))
            {
                throw new InvalidOperationException(
                    $"Bicep catalog metadata for '{descriptor.Type}.{propertyName}' requires a declared default.");
            }

            var actualValue = ReadPropertyValue(resource, propertyName);
            if (actualValue is { } actual && !JsonEquals(actual, defaultValue))
            {
                throw new InvalidOperationException(
                    $"Bicep generation does not support '{descriptor.Type}.{propertyName}' value '{FormatValue(actual)}' "
                    + "other than its catalog default: "
                    + unsupported.GetProperty("reason").GetString());
            }
        }
    }

    private static void ValidateRequirements(ResourcePlan resource, CatalogResourceTypeDescriptor descriptor)
    {
        var bicep = BicepCatalogMetadata.GetBicep(descriptor);
        if (!bicep.TryGetProperty("requirements", out var requirements))
        {
            return;
        }

        foreach (var requirement in requirements.EnumerateArray())
        {
            var condition = requirement.GetProperty("when");
            var conditionProperty = condition.GetProperty("property").GetString()!;
            var conditionValue = ReadPropertyValue(resource, conditionProperty);
            if (!JsonEquals(conditionValue, condition.GetProperty("equals")))
            {
                continue;
            }

            var propertyName = requirement.GetProperty("property").GetString()!;
            var value = ReadPropertyValue(resource, propertyName);
            if (requirement.TryGetProperty("allowedValues", out var allowedValues)
                && !allowedValues.EnumerateArray().Any(allowed => JsonEquals(value, allowed)))
            {
                throw new InvalidOperationException(requirement.GetProperty("reason").GetString());
            }

            if (requirement.TryGetProperty("minItems", out var minimumItems)
                && GetItemCount(value) < minimumItems.GetInt32())
            {
                throw new InvalidOperationException(requirement.GetProperty("reason").GetString());
            }

            if (requirement.TryGetProperty("disallowedValues", out var disallowedValues)
                && disallowedValues.EnumerateArray().Any(disallowed => ContainsValue(value, disallowed)))
            {
                throw new InvalidOperationException(requirement.GetProperty("reason").GetString());
            }
        }
    }

    private static void ValidateExposureSupport(ResourcePlan resource, CatalogResourceTypeDescriptor descriptor)
    {
        var bicep = BicepCatalogMetadata.GetBicep(descriptor);
        if (resource.Exposure is not { } exposure
            || !bicep.TryGetProperty("exposureSupport", out var support))
        {
            return;
        }

        if (!support.GetProperty("modes").EnumerateArray().Any(mode => string.Equals(
            mode.GetString(), exposure.Mode, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(support.GetProperty("reason").GetString());
        }
    }

    private static object? ReadPropertyValue(ResourcePlan resource, string propertyName)
    {
        if (string.Equals(propertyName, "exposure", StringComparison.Ordinal))
        {
            return resource.Exposure?.Mode;
        }

        if (string.Equals(propertyName, "allowedIpRanges", StringComparison.Ordinal))
        {
            return resource.Exposure?.AllowedIpRanges;
        }

        var value = resource.Properties.SingleOrDefault(property =>
            string.Equals(property.Name, propertyName, StringComparison.Ordinal))?.Value;
        return value is { } json ? json : null;
    }

    private static int GetItemCount(object? value) => value switch
    {
        IReadOnlyCollection<string> items => items.Count,
        JsonElement { ValueKind: JsonValueKind.Array } items => items.GetArrayLength(),
        _ => 0
    };

    private static bool ContainsValue(object? value, JsonElement expected) => value switch
    {
        string text => expected.ValueKind == JsonValueKind.String
            && string.Equals(text, expected.GetString(), StringComparison.Ordinal),
        IReadOnlyCollection<string> items => items.Any(item => expected.ValueKind == JsonValueKind.String
            && string.Equals(item, expected.GetString(), StringComparison.Ordinal)),
        JsonElement { ValueKind: JsonValueKind.Array } items => items.EnumerateArray()
            .Any(item => JsonElement.DeepEquals(item, expected)),
        JsonElement json => JsonElement.DeepEquals(json, expected),
        _ => false
    };

    private static string FormatValue(object value) => value switch
    {
        JsonElement json => json.GetRawText(),
        string text => text,
        _ => value.ToString() ?? string.Empty
    };

    private static bool JsonEquals(object? value, JsonElement expected) => value switch
    {
        null => expected.ValueKind == JsonValueKind.Null,
        string text => expected.ValueKind == JsonValueKind.String
            && string.Equals(text, expected.GetString(), StringComparison.Ordinal),
        JsonElement json => JsonElement.DeepEquals(json, expected),
        _ => false
    };
}
