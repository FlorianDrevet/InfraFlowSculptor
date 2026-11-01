using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace InfraFlowSculptor.Emitters.Bicep;

internal static class BicepValueWriter
{
    public static void WriteProperty(BicepWriter writer, string name, object? value)
    {
        switch (value)
        {
            case JsonElement element when element.ValueKind is JsonValueKind.Object:
                writer.BeginBlock($"{PropertyName(name)}: {{");
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    WriteProperty(writer, property.Name, property.Value);
                }

                writer.EndBlock();
                break;
            case JsonElement element when element.ValueKind is JsonValueKind.Array:
                if (element.GetArrayLength() == 0)
                {
                    writer.Line($"{PropertyName(name)}: []");
                    break;
                }

                writer.BeginArray($"{PropertyName(name)}: [");
                foreach (var item in element.EnumerateArray())
                {
                    WriteArrayValue(writer, item);
                }

                writer.EndArray();
                break;
            case IReadOnlyDictionary<string, object?> dictionary:
                writer.BeginBlock($"{PropertyName(name)}: {{");
                foreach (var pair in dictionary)
                {
                    WriteProperty(writer, pair.Key, pair.Value);
                }

                writer.EndBlock();
                break;
            case IReadOnlyDictionary<string, string> dictionary:
                writer.BeginBlock($"{PropertyName(name)}: {{");
                foreach (var pair in dictionary)
                {
                    WriteProperty(writer, pair.Key, pair.Value);
                }

                writer.EndBlock();
                break;
            case IEnumerable sequence when value is not string:
                if (!sequence.Cast<object?>().Any())
                {
                    writer.Line($"{PropertyName(name)}: []");
                    break;
                }

                writer.BeginArray($"{PropertyName(name)}: [");
                foreach (var item in sequence)
                {
                    WriteArrayValue(writer, item);
                }

                writer.EndArray();
                break;
            default:
                writer.Line($"{PropertyName(name)}: {FormatScalar(value)}");
                break;
        }
    }

    private static void WriteArrayValue(BicepWriter writer, object? value)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Object } objectElement)
        {
            writer.BeginBlock("{");
            foreach (var property in objectElement.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                WriteProperty(writer, property.Name, property.Value);
            }

            writer.EndBlock();
            return;
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            writer.BeginBlock("{");
            foreach (var pair in dictionary)
            {
                WriteProperty(writer, pair.Key, pair.Value);
            }

            writer.EndBlock();
            return;
        }

        if (value is IReadOnlyDictionary<string, string> stringDictionary)
        {
            writer.BeginBlock("{");
            foreach (var pair in stringDictionary)
            {
                WriteProperty(writer, pair.Key, pair.Value);
            }

            writer.EndBlock();
            return;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } array)
        {
            writer.BeginArray("[");
            foreach (var item in array.EnumerateArray())
            {
                WriteArrayValue(writer, item);
            }

            writer.EndArray();
            return;
        }

        if (value is IEnumerable sequence && value is not string && value is not JsonElement)
        {
            writer.BeginArray("[");
            foreach (var item in sequence)
            {
                WriteArrayValue(writer, item);
            }

            writer.EndArray();
            return;
        }

        writer.Line(FormatScalar(value));
    }

    private static string FormatScalar(object? value) => value switch
    {
        null => "null",
        JsonElement { ValueKind: JsonValueKind.String } element => BicepCatalogMetadata.SingleQuoted(element.GetString()!),
        JsonElement { ValueKind: JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False } element => element.GetRawText(),
        JsonElement { ValueKind: JsonValueKind.Null } => "null",
        string text => BicepCatalogMetadata.SingleQuoted(text),
        bool boolean => boolean ? "true" : "false",
        Guid guid => BicepCatalogMetadata.SingleQuoted(guid.ToString("D", CultureInfo.InvariantCulture)),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null"
    };

    private static string PropertyName(string name) => IsIdentifier(name)
        ? name
        : BicepCatalogMetadata.SingleQuoted(name);

    private static bool IsIdentifier(string name) =>
        name.Length > 0
        && (IsAsciiLetter(name[0]) || name[0] == '_')
        && name.Skip(1).All(character => IsAsciiLetter(character) || char.IsAsciiDigit(character) || character == '_');

    private static bool IsAsciiLetter(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
