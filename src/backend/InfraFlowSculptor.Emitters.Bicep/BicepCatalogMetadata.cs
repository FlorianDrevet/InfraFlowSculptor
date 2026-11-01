using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.Bicep;

internal static class BicepCatalogMetadata
{
    public static void EnsureNoUnresolvedPlaceholders(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var unresolved = Regex.Match(template, @"\{\{[^{}]*\}\}", RegexOptions.CultureInvariant);
        if (unresolved.Success)
        {
            throw new InvalidOperationException(
                $"Bicep catalog template contains an unresolved placeholder '{unresolved.Value}'.");
        }

        if (template.Contains("{{", StringComparison.Ordinal)
            || template.Contains("}}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Bicep catalog template contains a malformed placeholder.");
        }
    }

    public static JsonElement GetBicep(CatalogResourceTypeDescriptor descriptor) =>
        descriptor.Definition.GetProperty("generation").GetProperty("Bicep");

    public static JsonElement? TryGetBicep(CatalogResourceTypeDescriptor descriptor, string propertyName) =>
        GetBicep(descriptor).TryGetProperty(propertyName, out var value) ? value : null;

    public static JsonElement? TryGetExisting(CatalogResourceTypeDescriptor descriptor) =>
        TryGetBicep(descriptor, "existing");

    public static JsonElement? TryGetConfig(CatalogResourceTypeDescriptor descriptor) =>
        TryGetBicep(descriptor, "config");

    public static bool IsEmbedded(CatalogResourceTypeDescriptor descriptor) =>
        TryGetBicep(descriptor, "embedded") is not null;

    public static bool HasRestrictedExposure(ComponentPlan component, string resourceType) =>
        component.Targets.SelectMany(target => target.Resources).Any(resource =>
            string.Equals(resource.Type, resourceType, StringComparison.Ordinal)
            && string.Equals(resource.Exposure?.Mode, "Restricted", StringComparison.Ordinal));

    public static bool ShouldIncludeField(JsonElement field, ComponentPlan component, string resourceType)
    {
        var includeWhen = ReadString(field, "includeWhen");
        return includeWhen switch
        {
            null => true,
            "hasRestrictedExposure" => HasRestrictedExposure(component, resourceType),
            _ => throw new InvalidOperationException($"Unsupported Bicep field condition '{includeWhen}'.")
        };
    }

    public static string? ReadString(JsonElement definition, string propertyName) =>
        definition.ValueKind == JsonValueKind.Object
        && definition.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static int ReadInt32(JsonElement definition, string propertyName, int fallback = 0) =>
        definition.ValueKind == JsonValueKind.Object
        && definition.TryGetProperty(propertyName, out var value)
        && value.TryGetInt32(out var number)
            ? number
            : fallback;

    public static string SingleQuoted(string value)
    {
        var escaped = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            switch (character)
            {
                case '\\':
                    escaped.Append("\\\\");
                    break;
                case '\'':
                    escaped.Append("\\'");
                    break;
                case '\n':
                    escaped.Append("\\n");
                    break;
                case '\r':
                    escaped.Append("\\r");
                    break;
                case '\t':
                    escaped.Append("\\t");
                    break;
                case '$' when index + 1 < value.Length && value[index + 1] == '{':
                    escaped.Append("\\$");
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        escaped.Append("\\u{")
                            .Append(((int)character).ToString("X", CultureInfo.InvariantCulture))
                            .Append('}');
                    }
                    else
                    {
                        escaped.Append(character);
                    }

                    break;
            }
        }

        return $"'{escaped}'";
    }
}
