using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Values;

namespace InfraFlowSculptor.Engine.Exposure;

public sealed class ExposureResolver(ExposureHandlerRegistry handlers)
{
    private const string ExposureProperty = "exposure";
    private const string AllowedIpRangesProperty = "allowedIpRanges";

    public Exposure? Resolve(ResourceSnapshot resource, Guid targetId, CatalogResourceTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(descriptor);

        if (resource.IsExisting || !SupportsExposure(descriptor))
        {
            return null;
        }

        var values = EffectiveValueResolver.Resolve(resource, targetId, descriptor.Definition)
            .ToDictionary(value => value.Name, StringComparer.Ordinal);
        var modeValue = values.TryGetValue(ExposureProperty, out var modeProperty)
            && modeProperty.Value is { ValueKind: JsonValueKind.String } modeElement
                ? modeElement.GetString()
                : nameof(ExposureMode.Public);

        if (modeValue is null
            || !Enum.TryParse<ExposureMode>(modeValue, ignoreCase: false, out var mode)
            || !Enum.IsDefined(mode)
            || !string.Equals(Enum.GetName(mode), modeValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Exposure mode '{modeValue}' is not supported.");
        }

        IReadOnlyList<string> ranges = mode == ExposureMode.Restricted
            ? ReadRanges(values)
            : Array.Empty<string>();
        return handlers.Get(mode).Build(ranges);
    }

    private static bool SupportsExposure(CatalogResourceTypeDescriptor descriptor) =>
        descriptor.Definition.GetProperty("properties").EnumerateArray().Any(property =>
            string.Equals(property.GetProperty("name").GetString(), ExposureProperty, StringComparison.Ordinal));

    private static List<string> ReadRanges(Dictionary<string, EffectivePropertyValue> values)
    {
        if (!values.TryGetValue(AllowedIpRangesProperty, out var property)
            || property.Value is not { } rangeValue
            || rangeValue.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (rangeValue.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Property '{AllowedIpRangesProperty}' must be an array.");
        }

        var ranges = new List<string>(rangeValue.GetArrayLength());
        foreach (var range in rangeValue.EnumerateArray())
        {
            if (range.ValueKind != JsonValueKind.String || range.GetString() is not { } value)
            {
                throw new InvalidOperationException($"Property '{AllowedIpRangesProperty}' contains a non-string value.");
            }

            ranges.Add(value);
        }

        return ranges;
    }
}
