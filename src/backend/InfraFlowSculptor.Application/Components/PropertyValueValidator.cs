using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using ErrorOr;
using InfraFlowSculptor.Catalog;

namespace InfraFlowSculptor.Application.Components;

public sealed class PropertyValueValidator
{
    public static ErrorOr<Success> Validate(
        CatalogResourceTypeDescriptor descriptor,
        IReadOnlyDictionary<string, JsonElement> values,
        bool isPublished = false,
        IReadOnlyDictionary<string, JsonElement>? currentValues = null)
    {
        var definitions = descriptor.Definition.GetProperty("properties");
        return ValidateDefinitions(
            definitions,
            values,
            isPublished,
            currentValues is null ? null : ResolveEffectiveValues(definitions, currentValues));
    }

    public static ErrorOr<Success> ValidateOverride(
        CatalogResourceTypeDescriptor descriptor,
        string propertyName,
        JsonElement value,
        bool isPublished = false,
        IReadOnlyDictionary<string, JsonElement>? currentValues = null)
    {
        var definition = FindProperty(descriptor, propertyName);
        if (definition is null)
        {
            return Error.Validation("RESOURCE_PROPERTY_UNKNOWN", $"Property '{propertyName}' is not declared by the resource type.");
        }

        if (!definition.Value.TryGetProperty("overridable", out var overridable) || !overridable.GetBoolean())
        {
            return Error.Validation("RESOURCE_PROPERTY_NOT_OVERRIDABLE", $"Property '{propertyName}' cannot be overridden.");
        }

        return ValidateValue(
            propertyName,
            value,
            definition.Value,
            isPublished,
            currentValues is null
                ? null
                : ResolveEffectiveValues(descriptor.Definition.GetProperty("properties"), currentValues));
    }

    public static ErrorOr<Success> ValidateOverrideClear(
        CatalogResourceTypeDescriptor descriptor,
        string propertyName,
        IReadOnlyDictionary<string, JsonElement> currentEffectiveValues,
        IReadOnlyDictionary<string, JsonElement> inheritedValues,
        bool isPublished = false)
    {
        var definition = FindProperty(descriptor, propertyName);
        if (definition is null)
        {
            return Error.Validation("RESOURCE_PROPERTY_UNKNOWN", $"Property '{propertyName}' is not declared by the resource type.");
        }

        if (!definition.Value.TryGetProperty("overridable", out var overridable) || !overridable.GetBoolean())
        {
            return Error.Validation("RESOURCE_PROPERTY_NOT_OVERRIDABLE", $"Property '{propertyName}' cannot be overridden.");
        }

        var nextValue = inheritedValues.TryGetValue(propertyName, out var inheritedValue)
            ? inheritedValue
            : GetNullValue();
        return ValidateValue(propertyName, nextValue, definition.Value, isPublished, currentEffectiveValues);
    }

    public static ErrorOr<Success> ValidateChildProperties(
        CatalogResourceTypeDescriptor descriptor,
        string childType,
        IReadOnlyDictionary<string, JsonElement> values,
        bool isPublished = false,
        IReadOnlyDictionary<string, JsonElement>? currentValues = null)
    {
        foreach (var child in descriptor.Definition.GetProperty("children").EnumerateArray())
        {
            if (string.Equals(child.GetProperty("type").GetString(), childType, StringComparison.Ordinal))
            {
                var definitions = child.GetProperty("properties");
                return ValidateDefinitions(
                    definitions,
                    values,
                    isPublished,
                    currentValues is null ? null : ResolveEffectiveValues(definitions, currentValues));
            }
        }

        return Error.Validation("RESOURCE_CHILD_TYPE_UNKNOWN", $"Child type '{childType}' is not declared by the resource descriptor.");
    }

    private static ErrorOr<Success> ValidateDefinitions(
        JsonElement definitions,
        IReadOnlyDictionary<string, JsonElement> values,
        bool isPublished,
        IReadOnlyDictionary<string, JsonElement>? currentValues)
    {
        foreach (var (name, value) in values)
        {
            JsonElement? propertyDefinition = definitions.EnumerateArray()
                .FirstOrDefault(item => string.Equals(item.GetProperty("name").GetString(), name, StringComparison.Ordinal));
            if (propertyDefinition is null || propertyDefinition.Value.ValueKind == JsonValueKind.Undefined)
            {
                return Error.Validation("RESOURCE_PROPERTY_UNKNOWN", $"Property '{name}' is not declared by the resource type.");
            }

            var valid = ValidateValue(name, value, propertyDefinition.Value, isPublished, currentValues);
            if (valid.IsError)
            {
                return valid.Errors;
            }
        }

        return Result.Success;
    }

    public static IReadOnlyDictionary<string, JsonElement> ResolveEffectiveValues(
        CatalogResourceTypeDescriptor descriptor,
        IReadOnlyDictionary<string, JsonElement>? baseValues = null,
        IReadOnlyDictionary<string, JsonElement>? overrides = null) =>
        ResolveEffectiveValues(descriptor.Definition.GetProperty("properties"), baseValues, overrides);

    private static Dictionary<string, JsonElement> ResolveEffectiveValues(
        JsonElement definitions,
        IReadOnlyDictionary<string, JsonElement>? baseValues,
        IReadOnlyDictionary<string, JsonElement>? overrides = null)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var definition in definitions.EnumerateArray())
        {
            var name = definition.GetProperty("name").GetString()!;
            result[name] = definition.TryGetProperty("default", out var defaultValue)
                ? defaultValue.Clone()
                : GetNullValue();
        }

        if (baseValues is not null)
        {
            foreach (var (name, value) in baseValues)
            {
                result[name] = value.Clone();
            }
        }

        if (overrides is not null)
        {
            foreach (var (name, value) in overrides)
            {
                result[name] = value.Clone();
            }
        }

        return result;
    }

    private static JsonElement GetNullValue()
    {
        using var document = JsonDocument.Parse("null");
        return document.RootElement.Clone();
    }

    private static ErrorOr<Success> ValidateValue(
        string name,
        JsonElement value,
        JsonElement definition,
        bool isPublished,
        IReadOnlyDictionary<string, JsonElement>? currentValues)
    {
        if (value.ValueKind != JsonValueKind.Null && !MatchesKind(value, definition.GetProperty("kind").GetString()!))
        {
            return Error.Validation("RESOURCE_PROPERTY_TYPE_INVALID", $"Property '{name}' does not match its descriptor type.");
        }

        var current = default(JsonElement);
        var currentExists = currentValues is not null && currentValues.TryGetValue(name, out current);
        if (isPublished
            && definition.TryGetProperty("lockedAfterPublish", out var locked)
            && locked.GetBoolean()
            && currentExists
            && !JsonElement.DeepEquals(current, value))
        {
            return Error.Validation("RESOURCE_PROPERTY_LOCKED", $"Property '{name}' is locked after publication because Azure cannot change it without recreating the resource.");
        }

        if (currentExists
            && value.ValueKind != JsonValueKind.Null
            && definition.TryGetProperty("irreversible", out var irreversible)
            && JsonElement.DeepEquals(current, irreversible.GetProperty("to"))
            && JsonElement.DeepEquals(value, irreversible.GetProperty("from")))
        {
            return Error.Validation("RESOURCE_PROPERTY_IRREVERSIBLE", $"Property '{name}' cannot be changed back after its irreversible transition.");
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return Result.Success;
        }

        var kind = definition.GetProperty("kind").GetString()!;
        if (kind is "integer" or "number")
        {
            var number = value.GetDecimal();
            if (ReadDecimal(definition, "minimum") is { } minimum && number < minimum
                || ReadDecimal(definition, "maximum") is { } maximum && number > maximum)
            {
                return Error.Validation("RESOURCE_PROPERTY_RANGE", $"Property '{name}' is outside its descriptor range.");
            }
        }

        if (kind is "string" or "enum" or "reference")
        {
            var text = value.GetString()!;
            if (ReadInteger(definition, "minLength") is { } minLength && text.Length < minLength
                || ReadInteger(definition, "maxLength") is { } maxLength && text.Length > maxLength)
            {
                return Error.Validation("RESOURCE_PROPERTY_LENGTH", $"Property '{name}' is outside its descriptor length limits.");
            }

            if (definition.TryGetProperty("pattern", out var patternElement)
                && !MatchesPattern(text, patternElement.GetString()!))
            {
                return Error.Validation("RESOURCE_PROPERTY_PATTERN", $"Property '{name}' does not match its descriptor pattern.");
            }

            if (definition.TryGetProperty("format", out var formatElement)
                && (kind != "string" || !MatchesFormat(text, formatElement.GetString()!)))
            {
                return Error.Validation("RESOURCE_PROPERTY_FORMAT", $"Property '{name}' does not match its descriptor format.");
            }

            if (definition.TryGetProperty("deprecatedValues", out var deprecatedValues)
                && deprecatedValues.EnumerateArray().Any(item => string.Equals(item.GetString(), text, StringComparison.Ordinal)))
            {
                return Error.Validation("RESOURCE_PROPERTY_DEPRECATED", $"Property '{name}' uses a deprecated value.");
            }
        }

        if (kind == "enum"
            && definition.TryGetProperty("values", out var allowedValues)
            && !allowedValues.EnumerateArray().Any(item => JsonElement.DeepEquals(item, value)))
        {
            return Error.Validation("RESOURCE_PROPERTY_ENUM_INVALID", $"Property '{name}' must use a value declared by its descriptor.");
        }

        if (kind == "array")
        {
            if (definition.TryGetProperty("maxItems", out var maxItems)
                && value.GetArrayLength() > maxItems.GetInt32())
            {
                return Error.Validation("RESOURCE_PROPERTY_ARRAY_LENGTH", $"Property '{name}' exceeds its descriptor item limit.");
            }

            if (definition.TryGetProperty("items", out var itemDefinition))
            {
                var itemKind = itemDefinition.GetProperty("kind").GetString()!;
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Null || !MatchesKind(item, itemKind))
                    {
                        return Error.Validation("RESOURCE_PROPERTY_ITEM_TYPE_INVALID", $"Property '{name}' contains an item that does not match its descriptor type.");
                    }

                    var itemResult = ValidateValue(name, item, itemDefinition, isPublished, currentValues: null);
                    if (itemResult.IsError)
                    {
                        return itemResult.Errors;
                    }
                }
            }
        }

        return Result.Success;
    }

    private static JsonElement? FindProperty(CatalogResourceTypeDescriptor descriptor, string name)
    {
        foreach (var definition in descriptor.Definition.GetProperty("properties").EnumerateArray())
        {
            if (string.Equals(definition.GetProperty("name").GetString(), name, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    private static bool MatchesKind(JsonElement value, string kind) => kind switch
    {
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
        "string" or "enum" or "reference" => value.ValueKind == JsonValueKind.String,
        "array" => value.ValueKind == JsonValueKind.Array,
        "object" => value.ValueKind == JsonValueKind.Object,
        _ => false
    };

    private static decimal? ReadDecimal(JsonElement definition, string name) =>
        definition.TryGetProperty(name, out var value) && value.TryGetDecimal(out var number) ? number : null;

    private static int? ReadInteger(JsonElement definition, string name) =>
        definition.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;

    private static bool MatchesPattern(string value, string pattern)
    {
        try
        {
            return Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool MatchesFormat(string value, string format) => format switch
    {
        "ipv4Cidr" => IsCanonicalIpv4Cidr(value),
        _ => false
    };

    private static bool IsCanonicalIpv4Cidr(string value)
    {
        var slashIndex = value.IndexOf('/');
        if (slashIndex <= 0 || slashIndex != value.LastIndexOf('/'))
        {
            return false;
        }

        var prefixText = value.AsSpan(slashIndex + 1);
        if (prefixText.Length == 0
            || prefixText.Length > 1 && prefixText[0] == '0'
            || !int.TryParse(prefixText, NumberStyles.None, CultureInfo.InvariantCulture, out var prefix)
            || prefix > 32)
        {
            return false;
        }

        var octets = value[..slashIndex].Split('.');
        if (octets.Length != 4)
        {
            return false;
        }

        uint address = 0;
        foreach (var octetText in octets)
        {
            if (octetText.Length == 0
                || octetText.Length > 1 && octetText[0] == '0'
                || !int.TryParse(octetText, NumberStyles.None, CultureInfo.InvariantCulture, out var octet)
                || octet > 255)
            {
                return false;
            }

            address = (address << 8) | (uint)octet;
        }

        var networkMask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        return (address & ~networkMask) == 0;
    }
}
