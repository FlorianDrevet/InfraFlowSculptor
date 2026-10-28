using System.Text.Json;
using System.Globalization;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Values;

public static class EffectiveValueResolver
{
    private const string RequiresConditionPrefix = "requires:";
    private const string RequiredWhenConditionPrefix = "requiredWhen:";

    public static IReadOnlyList<EffectivePropertyValue> Resolve(
        ResourceSnapshot resource,
        Guid targetId,
        JsonElement descriptor)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (descriptor.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("A resource type descriptor must be a JSON object.", nameof(descriptor));
        }

        if (resource.IsExisting)
        {
            return [];
        }

        if (!descriptor.TryGetProperty("properties", out var definitions)
            || definitions.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("A resource type descriptor must contain a properties array.", nameof(descriptor));
        }

        resource.Overrides.TryGetValue(targetId, out var overrides);
        var propertyDefinitions = new List<(string Name, JsonElement Definition)>(definitions.GetArrayLength());
        var definitionsByName = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var resolvedValues = new Dictionary<string, EffectivePropertyValue>(StringComparer.Ordinal);
        foreach (var definition in definitions.EnumerateArray())
        {
            if (definition.ValueKind != JsonValueKind.Object
                || !definition.TryGetProperty("name", out var nameElement)
                || nameElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(nameElement.GetString()))
            {
                throw new ArgumentException("A resource property definition must have a non-empty string name.", nameof(descriptor));
            }

            var name = nameElement.GetString()!;

            propertyDefinitions.Add((name, definition));
            definitionsByName.Add(name, definition);
            resolvedValues.Add(name, ResolveValue(name, definition, overrides, resource.Properties));
        }

        return propertyDefinitions
            .Where(property => IsApplicable(property.Name, definitionsByName, resolvedValues, []))
            .Select(property => resolvedValues[property.Name])
            .ToArray();
    }

    private static EffectivePropertyValue ResolveValue(
        string name,
        JsonElement definition,
        IReadOnlyDictionary<string, JsonElement>? overrides,
        IReadOnlyDictionary<string, JsonElement> resourceProperties)
    {
        if (IsOverridable(definition) && TryGetStoredValue(overrides, name, out var overrideValue))
        {
            return Create(name, overrideValue, EffectiveValueProvenance.Override);
        }

        if (TryGetStoredValue(resourceProperties, name, out var resourceValue))
        {
            return Create(name, resourceValue, EffectiveValueProvenance.Resource);
        }

        return definition.TryGetProperty("default", out var defaultValue)
            ? Create(name, defaultValue, EffectiveValueProvenance.CatalogDefault)
            : new EffectivePropertyValue(name, null, EffectiveValueProvenance.Absent);
    }

    private static bool IsOverridable(JsonElement definition) =>
        definition.TryGetProperty("overridable", out var overridable)
        && overridable.ValueKind == JsonValueKind.True;

    private static bool IsApplicable(
        string propertyName,
        IReadOnlyDictionary<string, JsonElement> definitionsByName,
        IReadOnlyDictionary<string, EffectivePropertyValue> resolvedValues,
        HashSet<string> evaluationPath)
    {
        if (!definitionsByName.TryGetValue(propertyName, out var definition))
        {
            return false;
        }

        if (!evaluationPath.Add(propertyName))
        {
            throw new ArgumentException("Resource property conditions cannot contain a dependency cycle.", nameof(definitionsByName));
        }

        try
        {
            if (!definition.TryGetProperty("condition", out var conditionElement))
            {
                return true;
            }

            if (conditionElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("A resource property condition must be a string.", nameof(definitionsByName));
            }

            var condition = conditionElement.GetString();
            var conditionPrefix = condition?.StartsWith(RequiresConditionPrefix, StringComparison.Ordinal) == true
                ? RequiresConditionPrefix
                : condition?.StartsWith(RequiredWhenConditionPrefix, StringComparison.Ordinal) == true
                    ? RequiredWhenConditionPrefix
                    : null;
            if (conditionPrefix is null)
            {
                throw new ArgumentException("A resource property condition must use a supported prefix.", nameof(definitionsByName));
            }

            var requirement = condition![conditionPrefix.Length..];
            var equalsIndex = requirement.IndexOf('=');
            var dependencyName = equalsIndex < 0 ? requirement : requirement[..equalsIndex];
            if (string.IsNullOrWhiteSpace(dependencyName) || !definitionsByName.ContainsKey(dependencyName))
            {
                throw new ArgumentException("A resource property condition must reference a known property.", nameof(definitionsByName));
            }

            if (!IsApplicable(dependencyName, definitionsByName, resolvedValues, evaluationPath)
                || !resolvedValues.TryGetValue(dependencyName, out var dependency))
            {
                return false;
            }

            if (equalsIndex < 0)
            {
                return HasValue(dependency.Value);
            }

            var expectedValue = requirement[(equalsIndex + 1)..];
            return MatchesValue(dependency.Value, expectedValue);
        }
        finally
        {
            evaluationPath.Remove(propertyName);
        }
    }

    private static bool MatchesValue(JsonElement? value, string expectedValue)
    {
        if (!HasValue(value))
        {
            return false;
        }

        var element = value!.Value;
        if (element.ValueKind == JsonValueKind.Number)
        {
            return AreNumericValuesEqual(element.GetRawText(), expectedValue);
        }

        return TryGetScalarValue(element, out var actualValue)
            && string.Equals(actualValue, expectedValue, StringComparison.Ordinal);
    }

    private static bool HasValue(JsonElement? value) =>
        value.HasValue
        && value.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

    private static bool AreNumericValuesEqual(string actualValue, string expectedValue) =>
        TryParseNumber(actualValue, out var actualNumber)
        && TryParseNumber(expectedValue, out var expectedNumber)
        && actualNumber == expectedNumber;

    private static bool TryParseNumber(string value, out NormalizedNumber number)
    {
        number = default;
        var exponentIndex = value.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex < 0 ? value : value[..exponentIndex];
        var exponent = 0L;
        if (exponentIndex >= 0
            && !long.TryParse(value[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
        {
            return false;
        }

        var decimalIndex = mantissa.IndexOf('.');
        var fractionalDigits = decimalIndex < 0 ? 0 : mantissa.Length - decimalIndex - 1;
        var digits = decimalIndex < 0 ? mantissa : string.Concat(mantissa.AsSpan(0, decimalIndex), mantissa.AsSpan(decimalIndex + 1));
        var isNegative = digits.Length > 0 && digits[0] == '-';
        if (isNegative)
        {
            digits = digits[1..];
        }

        if (digits.Length == 0 || digits.Any(character => character is < '0' or > '9'))
        {
            return false;
        }

        var firstSignificantDigit = 0;
        while (firstSignificantDigit < digits.Length && digits[firstSignificantDigit] == '0')
        {
            firstSignificantDigit++;
        }

        if (firstSignificantDigit == digits.Length)
        {
            number = new NormalizedNumber(false, "0", 0);
            return true;
        }

        var significantDigitsEnd = digits.Length;
        while (significantDigitsEnd > firstSignificantDigit && digits[significantDigitsEnd - 1] == '0')
        {
            significantDigitsEnd--;
        }

        try
        {
            exponent = checked(exponent - fractionalDigits + (digits.Length - significantDigitsEnd));
        }
        catch (OverflowException)
        {
            return false;
        }

        number = new NormalizedNumber(
            isNegative,
            digits[firstSignificantDigit..significantDigitsEnd],
            exponent);
        return true;
    }

    private readonly record struct NormalizedNumber(bool IsNegative, string SignificantDigits, long Exponent);

    private static bool TryGetScalarValue(JsonElement value, out string scalarValue)
    {
        scalarValue = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            _ => string.Empty
        };
        return value.ValueKind is JsonValueKind.String or JsonValueKind.True or JsonValueKind.False;
    }

    private static bool TryGetStoredValue(
        IReadOnlyDictionary<string, JsonElement>? values,
        string propertyName,
        out JsonElement value)
    {
        if (values is not null && values.TryGetValue(propertyName, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static EffectivePropertyValue Create(
        string name,
        JsonElement value,
        EffectiveValueProvenance provenance) => value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? new EffectivePropertyValue(name, null, EffectiveValueProvenance.Absent)
            : new EffectivePropertyValue(name, value.Clone(), provenance);
}
