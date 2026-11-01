using System.Globalization;
using System.Text.Json;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.Bicep;

internal static class ParamFileEmitter
{
    public static GeneratedFile Emit(
        DeploymentPlan plan,
        ComponentPlan component,
        TargetPlan target,
        CatalogVersion catalog)
    {
        var writer = new BicepWriter();
        WriteHeader(writer, plan.Project.Code);
        writer.Line("using 'main.bicep'");
        writer.BlankLine();

        writer.BeginBlock("param target = {");
        BicepValueWriter.WriteProperty(writer, "code", target.Target.Code);
        BicepValueWriter.WriteProperty(writer, "subscriptionId", target.Target.SubscriptionId.ToString("D", CultureInfo.InvariantCulture));
        BicepValueWriter.WriteProperty(writer, "location", target.Target.Region);
        writer.EndBlock();
        writer.BlankLine();

        writer.BeginBlock("param resourceGroups = {");
        foreach (var group in target.ResourceGroups.OrderBy(group => group.LogicalName, StringComparer.Ordinal))
        {
            writer.BeginBlock($"{BicepIdentifier.FromLogicalName(group.LogicalName)}: {{");
            BicepValueWriter.WriteProperty(writer, "name", group.Name);
            BicepValueWriter.WriteProperty(writer, "location", group.Region);
            BicepValueWriter.WriteProperty(writer, "tags", OrderTags(group.Tags));
            writer.EndBlock();
        }

        writer.EndBlock();
        writer.BlankLine();

        var externalResources = BuildExternalResources(target, catalog);
        if (externalResources.Count > 0)
        {
            if (externalResources.Count == 1)
            {
                var pair = externalResources.Single();
                writer.BeginBlock($"param {pair.Key} = {{");
                if (pair.Value is IReadOnlyDictionary<string, object?> values)
                {
                    foreach (var value in values)
                    {
                        BicepValueWriter.WriteProperty(writer, value.Key, value.Value);
                    }
                }

                writer.EndBlock();
            }
            else
            {
                writer.BeginBlock("param externalResources = {");
                foreach (var pair in externalResources)
                {
                    BicepValueWriter.WriteProperty(writer, pair.Key, pair.Value);
                }

                writer.EndBlock();
            }

            writer.BlankLine();
        }

        var resources = target.Resources.Where(resource => !resource.IsExisting)
            .Where(resource =>
            {
                var descriptor = catalog.GetResourceType(resource.Type)
                    ?? throw new InvalidOperationException(
                        $"The pinned catalog has no descriptor for resource type '{resource.Type}'.");
                return !BicepCatalogMetadata.IsEmbedded(descriptor);
            })
            .Select(resource =>
            {
                var descriptor = catalog.GetResourceType(resource.Type)
                    ?? throw new InvalidOperationException(
                        $"The pinned catalog has no descriptor for resource type '{resource.Type}'.");
                var definition = BicepCatalogMetadata.TryGetConfig(descriptor)
                    ?? throw new InvalidOperationException(
                        $"Resource type '{descriptor.Type}' has no Bicep configuration metadata.");
                return (Resource: resource, Descriptor: descriptor, Config: definition);
            })
            .Where(entry => entry.Config.TryGetProperty("fields", out _))
            .DistinctBy(entry => entry.Resource.Type)
            .OrderBy(entry => BicepCatalogMetadata.ReadInt32(entry.Config, "order"))
            .ThenBy(entry => BicepCatalogMetadata.ReadString(entry.Config, "typeName"), StringComparer.Ordinal)
            .ToArray();

        for (var resourceIndex = 0; resourceIndex < resources.Length; resourceIndex++)
        {
            var entry = resources[resourceIndex];
            var parameterName = BicepCatalogMetadata.ReadString(
                BicepCatalogMetadata.GetBicep(entry.Descriptor), "parameterName")
                ?? throw new InvalidOperationException(
                    $"Resource type '{entry.Descriptor.Type}' has no Bicep parameter name.");
            writer.BeginBlock($"param {parameterName} = {{");
            foreach (var field in entry.Config.GetProperty("fields").EnumerateArray())
            {
                if (!BicepCatalogMetadata.ShouldIncludeField(field, component, entry.Resource.Type))
                {
                    continue;
                }

                var fieldName = BicepCatalogMetadata.ReadString(field, "name")!;
                var source = field.GetProperty("source");
                BicepValueWriter.WriteProperty(writer, fieldName,
                    ResolveValue(source, BicepCatalogMetadata.ReadString(field, "transform"),
                        entry.Resource, target, entry.Descriptor, catalog));
            }

            writer.EndBlock();
            if (resourceIndex < resources.Length - 1)
            {
                writer.BlankLine();
            }
        }

        return new GeneratedFile(
            $"{component.ComponentCode}/infra/main.{target.Target.Code}.bicepparam",
            writer.ToString().TrimEnd('\n') + "\n");
    }

    private static object? ResolveValue(
        JsonElement source,
        string? transform,
        ResourcePlan resource,
        TargetPlan target,
        CatalogResourceTypeDescriptor descriptor,
        CatalogVersion catalog)
    {
        var kind = BicepCatalogMetadata.ReadString(source, "kind");
        var field = BicepCatalogMetadata.ReadString(source, "field");
        object? value = kind switch
        {
            "resource" => ResolveResourceField(resource, field),
            "property" => ReadProperty(resource.Properties, field),
            "fixedValue" => ReadProperty(resource.FixedValues, field),
            "constant" => source.TryGetProperty("value", out var constant) ? constant.Clone() : null,
            "exposure" => field switch
            {
                "mode" or "exposure" => resource.Exposure?.Mode,
                "allowedIpRanges" => resource.Exposure?.AllowedIpRanges ?? [],
                _ => throw new InvalidOperationException($"Unsupported Bicep exposure field '{field}'.")
            },
            "diagnostics" => ResolveDiagnostics(resource, target, field),
            "applicationSetting" => target.AppSettings.SingleOrDefault(setting =>
                string.Equals(setting.Name, field, StringComparison.Ordinal))?.SecretName ?? string.Empty,
            "child" => ResolveChild(resource, target, descriptor, catalog, field),
            "childDiagnostics" => ResolveChildDiagnostics(resource, target, descriptor, catalog),
            _ => throw new InvalidOperationException($"Unsupported Bicep configuration source '{kind}'.")
        };

        if (transform is null)
        {
            return value;
        }

        return transform switch
        {
            "numberToString" => NumberToString(value),
            "externalIngress" => ReadStringValue(value) is { } ingress
                && string.Equals(ingress, "External", StringComparison.Ordinal),
            "publicExposureEnabled" => string.Equals(ReadStringValue(value), "Public", StringComparison.Ordinal)
                ? "Enabled"
                : "Disabled",
            "publicNetworkAccessEnabled" => ReadStringValue(value) is "Public" or "Restricted"
                ? "Enabled"
                : throw new InvalidOperationException(
                    $"Cannot resolve SQL public network access from exposure mode '{ReadStringValue(value) ?? "<missing>"}'."),
            "restrictedNetworkDefaultAction" => ReadStringValue(value) switch
            {
                "Public" => "Allow",
                "Restricted" => "Deny",
                var mode => throw new InvalidOperationException(
                    $"Cannot resolve the ACR network rule action for exposure mode '{mode}'.")
            },
            _ => throw new InvalidOperationException($"Unsupported Bicep value transform '{transform}'.")
        };
    }

    private static object? ResolveResourceField(ResourcePlan resource, string? field) => field switch
    {
        "present" => resource.Present,
        "name" => resource.Name,
        "location" => resource.Region,
        "tags" => OrderTags(resource.Tags),
        _ => throw new InvalidOperationException($"Unsupported Bicep resource field '{field}'.")
    };

    private static JsonElement? ReadProperty(IReadOnlyList<ResourcePropertyPlan> properties, string? name) =>
        properties.SingleOrDefault(property => string.Equals(property.Name, name, StringComparison.Ordinal))?.Value;

    private static IReadOnlyDictionary<string, object?>[] ResolveDiagnostics(
        ResourcePlan resource,
        TargetPlan target,
        string? field) => target.DiagnosticSettings
        .Where(setting => string.Equals(setting.SourceResourceType, resource.Type, StringComparison.Ordinal)
            && string.Equals(setting.SourceLogicalName, resource.LogicalName, StringComparison.Ordinal))
        .Select(setting =>
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = setting.Name
            };
            if (string.Equals(field, "logs", StringComparison.Ordinal) && setting.LogCategoryGroup is not null)
            {
                values["logCategoryGroup"] = setting.LogCategoryGroup;
                values["metricCategory"] = setting.MetricCategory;
                values["logAnalyticsDestinationType"] = setting.LogAnalyticsDestinationType;
            }
            else if (string.Equals(field, "metrics", StringComparison.Ordinal))
            {
                values["metricCategory"] = setting.MetricCategory;
                values["logAnalyticsDestinationType"] = setting.LogAnalyticsDestinationType;
            }

            return (IReadOnlyDictionary<string, object?>)values;
        })
        .ToArray();

    private static object? ResolveChild(
        ResourcePlan parent,
        TargetPlan target,
        CatalogResourceTypeDescriptor parentDescriptor,
        CatalogVersion catalog,
        string? field)
    {
        var childType = GetEmbeddedChildType(parentDescriptor);
        if (childType is null)
        {
            return null;
        }

        var child = FindChild(parent, target.Resources, childType);
        if (child is null)
        {
            if (parent.Present)
            {
                throw new InvalidOperationException(
                    $"Bicep catalog metadata requires an embedded '{childType}' child for present '{parent.Type}' resource '{parent.LogicalName}'.");
            }

            if (string.Equals(field, "name", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var childDescriptor = catalog.GetResourceType(childType)
                ?? throw new InvalidOperationException(
                    $"The pinned catalog has no descriptor for embedded resource type '{childType}'.");
            var childProperty = childDescriptor.Definition.GetProperty("properties").EnumerateArray()
                .SingleOrDefault(property => string.Equals(
                    BicepCatalogMetadata.ReadString(property, "name"), field, StringComparison.Ordinal));
            return childProperty.ValueKind == JsonValueKind.Object
                && childProperty.TryGetProperty("default", out var defaultValue)
                    ? defaultValue.Clone()
                    : throw new InvalidOperationException(
                        $"Bicep catalog metadata cannot provide a default for missing embedded field '{childType}.{field}'.");
        }

        return field == "name" ? child.Name : ReadProperty(child.Properties, field);
    }

    private static IReadOnlyDictionary<string, object?>[] ResolveChildDiagnostics(
        ResourcePlan parent,
        TargetPlan target,
        CatalogResourceTypeDescriptor parentDescriptor,
        CatalogVersion catalog)
    {
        var childType = GetEmbeddedChildType(parentDescriptor);
        var child = childType is null ? null : FindChild(parent, target.Resources, childType);
        if (child is null && parent.Present && childType is not null)
        {
            throw new InvalidOperationException(
                $"Bicep catalog metadata requires an embedded '{childType}' child for present '{parent.Type}' resource '{parent.LogicalName}'.");
        }

        return child is null ? [] : ResolveDiagnostics(child, target, "logs");
    }

    private static ResourcePlan? FindChild(ResourcePlan parent, IReadOnlyList<ResourcePlan> resources, string childType)
    {
        var children = resources.Where(resource => string.Equals(resource.Type, childType, StringComparison.Ordinal)
            && resource.Dependencies.Any(dependency => dependency.Kind == "Hosting"
                && string.Equals(dependency.ResourceType, parent.Type, StringComparison.Ordinal)
                && string.Equals(dependency.LogicalName, parent.LogicalName, StringComparison.Ordinal)
                && (parent.ResourceId is null || string.Equals(dependency.ResourceId, parent.ResourceId, StringComparison.Ordinal))))
            .ToArray();
        if (children.Length > 1)
        {
            throw new InvalidOperationException(
                $"Bicep catalog metadata supports one embedded '{childType}' child for '{parent.Type}' resource '{parent.LogicalName}'.");
        }

        return children.SingleOrDefault();
    }

    private static string? GetEmbeddedChildType(CatalogResourceTypeDescriptor descriptor)
    {
        var bicep = BicepCatalogMetadata.GetBicep(descriptor);
        if (!bicep.TryGetProperty("embeddedChildren", out var children))
        {
            return null;
        }

        var childTypes = children.EnumerateArray()
            .Select(child => BicepCatalogMetadata.ReadString(child, "type"))
            .ToArray();
        if (childTypes.Length > 1)
        {
            throw new InvalidOperationException(
                $"Bicep catalog metadata supports one embedded child type for '{descriptor.Type}'.");
        }

        return childTypes.SingleOrDefault();
    }

    private static Dictionary<string, object?> BuildExternalResources(TargetPlan target, CatalogVersion catalog)
    {
        var roots = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var reference in target.ExternalReferences
            .OrderBy(reference => GetResourceOrder(catalog.GetResourceType(reference.ResourceType)))
            .ThenBy(reference => reference.ComponentCode, StringComparer.Ordinal)
            .ThenBy(reference => reference.LogicalName, StringComparer.Ordinal))
        {
            var descriptor = catalog.GetResourceType(reference.ResourceType)
                ?? throw new InvalidOperationException(
                    $"The pinned catalog has no descriptor for resource type '{reference.ResourceType}'.");
            var existing = BicepCatalogMetadata.TryGetExisting(descriptor)
                ?? throw new InvalidOperationException(
                    $"Resource type '{descriptor.Type}' has no Bicep existing-resource metadata.");
            var parameterPath = BicepCatalogMetadata.ReadString(existing, "parameterName")!;
            var segments = parameterPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var root = segments[0];
            if (!roots.TryGetValue(root, out var value))
            {
                roots[root] = value = new Dictionary<string, object?>(StringComparer.Ordinal);
            }

            var referenceValue = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["subscriptionId"] = reference.SubscriptionId.ToString("D", CultureInfo.InvariantCulture),
                ["resourceGroupName"] = reference.ResourceGroup,
                ["name"] = reference.Name
            };
            if (segments.Length == 1)
            {
                foreach (var pair in referenceValue)
                {
                    value[pair.Key] = pair.Value;
                }
            }
            else if (segments.Length == 2 && string.Equals(segments[1], "server", StringComparison.Ordinal))
            {
                value["server"] = referenceValue;
            }
            else if (segments.Length == 2 && string.Equals(segments[1], "name", StringComparison.Ordinal))
            {
                value["name"] = reference.Name;
            }
        }

        return roots.OrderBy(pair => GetExternalOrder(pair.Key, target, catalog))
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal);
    }

    private static int GetExternalOrder(string parameterName, TargetPlan target, CatalogVersion catalog) =>
        target.ExternalReferences
            .Select(reference => catalog.GetResourceType(reference.ResourceType))
            .Select(descriptor => GetExistingOrder(descriptor, parameterName))
            .Min();

    private static int GetResourceOrder(CatalogResourceTypeDescriptor? descriptor)
    {
        if (descriptor is null || BicepCatalogMetadata.TryGetExisting(descriptor) is not { } existing)
        {
            return int.MaxValue;
        }

        return BicepCatalogMetadata.ReadInt32(existing, "order");
    }

    private static object? NumberToString(object? value) => value is JsonElement element
        ? element.ValueKind switch
        {
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.String => element.GetString(),
            _ => value
        }
        : value;

    private static string? ReadStringValue(object? value) => value is JsonElement element
        && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : value as string;

    private static int GetExistingOrder(CatalogResourceTypeDescriptor? descriptor, string parameterName)
    {
        if (descriptor is null || BicepCatalogMetadata.TryGetExisting(descriptor) is not { } existing)
        {
            return int.MaxValue;
        }

        var path = BicepCatalogMetadata.ReadString(existing, "parameterName");
        return string.Equals(path?.Split('.')[0], parameterName, StringComparison.Ordinal)
            ? BicepCatalogMetadata.ReadInt32(existing, "order")
            : int.MaxValue;
    }

    private static Dictionary<string, string> OrderTags(IReadOnlyDictionary<string, string> tags)
    {
        var ordered = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in tags.Where(pair => !pair.Key.StartsWith("ifs-", StringComparison.Ordinal)
                && !string.Equals(pair.Key, "managed-by", StringComparison.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ordered.Add(pair.Key, pair.Value);
        }

        foreach (var key in new[] { "ifs-project", "ifs-component", "ifs-environment", "managed-by" })
        {
            if (tags.TryGetValue(key, out var value))
            {
                ordered.Add(key, value);
            }
        }

        foreach (var pair in tags.Where(pair => pair.Key.StartsWith("ifs-", StringComparison.Ordinal)
                && !new[] { "ifs-project", "ifs-component", "ifs-environment" }.Contains(pair.Key, StringComparer.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ordered.Add(pair.Key, pair.Value);
        }

        return ordered;
    }

    private static void WriteHeader(BicepWriter writer, string projectCode)
    {
        writer.Line($"// Généré par InfraFlowSculptor — projet {projectCode}. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.");
    }
}
