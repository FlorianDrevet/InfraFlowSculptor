using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace InfraFlowSculptor.Catalog;

public static class CatalogPropertyValueValidator
{
    public static string GetUniqueValueIdentityKey(JsonElement value)
    {
        var kind = value.ValueKind switch
        {
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.Null => "null",
            _ => value.ValueKind.ToString()
        };
        var canonicalValue = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number.ToString("G29", CultureInfo.InvariantCulture),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText()
        };

        return $"{kind}\0{canonicalValue}";
    }

    public static CatalogPropertyValueFailure? Validate(JsonElement value, JsonElement definition)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return definition.TryGetProperty("required", out var required)
                && required.GetBoolean()
                ? new CatalogPropertyValueFailure(
                    CatalogPropertyValueFailureKind.RequiredObjectMember,
                    definition.GetProperty("name").GetString())
                : null;
        }

        var kind = definition.GetProperty("kind").GetString()!;
        if (!MatchesKind(value, kind))
        {
            return new CatalogPropertyValueFailure(CatalogPropertyValueFailureKind.Type);
        }

        if (kind is "integer" or "number")
        {
            if (!value.TryGetDecimal(out var number))
            {
                return new CatalogPropertyValueFailure(CatalogPropertyValueFailureKind.Range);
            }

            if (ReadDecimal(definition, "minimum") is { } minimum && number < minimum
                || ReadDecimal(definition, "maximum") is { } maximum && number > maximum)
            {
                return new CatalogPropertyValueFailure(CatalogPropertyValueFailureKind.Range);
            }
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString() ?? string.Empty;
            if (ReadInteger(definition, "minLength") is { } minLength && text.Length < minLength
                || ReadInteger(definition, "maxLength") is { } maxLength && text.Length > maxLength)
            {
                return new CatalogPropertyValueFailure(CatalogPropertyValueFailureKind.Length);
            }

            if (definition.TryGetProperty("pattern", out var patternElement)
                && !MatchesPattern(text, patternElement.GetString()!))
            {
                return new CatalogPropertyValueFailure(CatalogPropertyValueFailureKind.Pattern);
            }

            if (definition.TryGetProperty("deprecatedValues", out var deprecatedValues)
                && deprecatedValues.EnumerateArray().Any(item => JsonElement.DeepEquals(item, value)))
            {
                return new CatalogPropertyValueFailure(CatalogPropertyValueFailureKind.Deprecated);
            }
        }

        if (definition.TryGetProperty("format", out var formatElement)
            && (value.ValueKind != JsonValueKind.String
                || !MatchesFormat(value.GetString()!, formatElement.GetString()!)))
        {
            return new CatalogPropertyValueFailure(CatalogPropertyValueFailureKind.Format);
        }

        if (definition.TryGetProperty("forbiddenValues", out var forbiddenValues)
            && forbiddenValues.EnumerateArray().Any(item => JsonElement.DeepEquals(item, value)))
        {
            return new CatalogPropertyValueFailure(CatalogPropertyValueFailureKind.Forbidden);
        }

        if (kind == "enum"
            && definition.TryGetProperty("values", out var allowedValues)
            && !allowedValues.EnumerateArray().Any(item => JsonElement.DeepEquals(item, value)))
        {
            return new CatalogPropertyValueFailure(CatalogPropertyValueFailureKind.EnumValue);
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            if (definition.TryGetProperty("maxItems", out var maxItems)
                && value.GetArrayLength() > maxItems.GetInt32())
            {
                return new CatalogPropertyValueFailure(CatalogPropertyValueFailureKind.ArrayLength);
            }

            if (definition.TryGetProperty("items", out var itemDefinition))
            {
                var uniqueMemberName = itemDefinition.TryGetProperty("uniqueBy", out var uniqueBy)
                    ? uniqueBy.GetString()
                    : null;
                var uniqueValues = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Null
                        || !MatchesKind(item, itemDefinition.GetProperty("kind").GetString()!))
                    {
                        return new CatalogPropertyValueFailure(CatalogPropertyValueFailureKind.ItemType);
                    }

                    var itemFailure = Validate(item, itemDefinition);
                    if (itemFailure is not null)
                    {
                        return itemFailure;
                    }

                    if (uniqueMemberName is not null
                        && item.ValueKind == JsonValueKind.Object
                        && item.TryGetProperty(uniqueMemberName, out var uniqueValue))
                    {
                        if (!uniqueValues.Add(GetUniqueValueIdentityKey(uniqueValue)))
                        {
                            return new CatalogPropertyValueFailure(
                                CatalogPropertyValueFailureKind.DuplicateItemValue,
                                uniqueMemberName);
                        }
                    }
                }
            }
        }

        if (value.ValueKind == JsonValueKind.Object
            && definition.TryGetProperty("members", out var members))
        {
            var memberDefinitions = members.EnumerateArray()
                .ToDictionary(member => member.GetProperty("name").GetString()!, StringComparer.Ordinal);
            var seenMemberNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!seenMemberNames.Add(property.Name))
                {
                    return new CatalogPropertyValueFailure(
                        CatalogPropertyValueFailureKind.DuplicateObjectMember,
                        property.Name);
                }

                if (!memberDefinitions.TryGetValue(property.Name, out var memberDefinition))
                {
                    return new CatalogPropertyValueFailure(
                        CatalogPropertyValueFailureKind.UnknownObjectMember,
                        property.Name);
                }

                var memberFailure = Validate(property.Value, memberDefinition);
                if (memberFailure is not null)
                {
                    return memberFailure;
                }
            }

            foreach (var (memberName, memberDefinition) in memberDefinitions)
            {
                if (memberDefinition.TryGetProperty("required", out var required)
                    && required.GetBoolean()
                    && !value.TryGetProperty(memberName, out _))
                {
                    return new CatalogPropertyValueFailure(
                        CatalogPropertyValueFailureKind.RequiredObjectMember,
                        memberName);
                }
            }
        }

        return null;
    }

    private static bool MatchesKind(JsonElement value, string kind) => kind switch
    {
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
        "string" or "reference" => value.ValueKind == JsonValueKind.String,
        "enum" => value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False,
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
        catch (ArgumentException)
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
