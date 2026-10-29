using System.Text.Json;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Targets;
using InfraFlowSculptor.Engine.Values;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class RestrictedExposureRule : IValidationRule
{
    private const string ExposureProperty = "exposure";
    private const string AllowedIpRangesProperty = "allowedIpRanges";

    public string Code => "VAL-NET-IP-VIDE";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var findings = new List<ValidationFinding>();
        foreach (var (component, resource) in context.Resources)
        {
            if (!context.ResourceTypes.TryGetValue(resource.Type, out var descriptor)
                || !descriptor.Definition.GetProperty("properties").EnumerateArray()
                    .Any(property => string.Equals(property.GetProperty("name").GetString(), ExposureProperty, StringComparison.Ordinal)))
            {
                continue;
            }

            foreach (var target in TargetResolver.ResolveResourceTargets(context.Snapshot, component, resource)
                         .Where(value => value.IsPresent))
            {
                var values = EffectiveValueResolver.Resolve(resource, target.Target.Id, descriptor.Definition)
                    .ToDictionary(value => value.Name, StringComparer.Ordinal);
                if (!values.TryGetValue(ExposureProperty, out var mode)
                    || mode.Value is not { ValueKind: JsonValueKind.String } modeValue
                    || !string.Equals(modeValue.GetString(), "Restricted", StringComparison.Ordinal))
                {
                    continue;
                }

                if (values.TryGetValue(AllowedIpRangesProperty, out var ranges)
                    && ranges.Value is { ValueKind: JsonValueKind.Array } rangeArray
                    && rangeArray.EnumerateArray().Any(range =>
                        range.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(range.GetString())))
                {
                    continue;
                }

                findings.Add(context.Finding(
                    Code,
                    Severity,
                    new ObjectRef(ObjectType.Resource, resource.Id),
                    $"L’exposition de « {resource.LogicalName} » est restreinte, mais aucune plage IP n’est définie.",
                    $"Exposure for '{resource.LogicalName}' is restricted, but no IP range is configured.",
                    target.Target.Id));
            }
        }

        return findings;
    }
}
