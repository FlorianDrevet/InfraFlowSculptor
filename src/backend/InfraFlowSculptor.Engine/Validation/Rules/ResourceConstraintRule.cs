using System.Globalization;
using System.Text.RegularExpressions;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Targets;
using InfraFlowSculptor.Engine.Values;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ResourceConstraintRule : IValidationRule
{
    private const string ExposureProperty = "exposure";

    public string Code => "VAL-RES-CONTRAINTE";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var findings = new List<ValidationFinding>();
        foreach (var (component, resource) in context.Resources)
        {
            if (!context.ResourceTypes.TryGetValue(resource.Type, out var descriptor))
            {
                continue;
            }

            var definitions = descriptor.Definition.GetProperty("properties").EnumerateArray()
                .ToDictionary(property => property.GetProperty("name").GetString()!, StringComparer.Ordinal);
            foreach (var target in TargetResolver.ResolveResourceTargets(context.Snapshot, component, resource)
                         .Where(value => value.IsPresent))
            {
                var resolution = EffectiveValueResolver.ResolveDetailed(resource, target.Target.Id, descriptor.Definition);
                foreach (var conditionIssue in resolution.UnresolvedConditions.Where(ShouldReportUnresolvedCondition))
                {
                    findings.Add(context.Finding(
                        Code,
                        Severity,
                        new ObjectRef(ObjectType.Resource, resource.Id),
                        $"La contrainte du catalogue pour « {conditionIssue.PropertyName} » ne peut pas être évaluée : elle dépend de « {conditionIssue.DependencyName} », qui n’est pas déclaré.",
                        $"Catalog constraint for '{conditionIssue.PropertyName}' cannot be evaluated because it depends on undeclared property '{conditionIssue.DependencyName}'.",
                        target.Target.Id));
                }

                foreach (var value in resolution.Values
                             .Where(value => value.Value is { ValueKind: not (System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined) }))
                {
                    if (!definitions.TryGetValue(value.Name, out var definition))
                    {
                        continue;
                    }

                    var reason = GetConstraintFailure(value.Value!.Value, definition);
                    if (reason is null)
                    {
                        continue;
                    }

                    findings.Add(context.Finding(
                        Code,
                        Severity,
                        new ObjectRef(ObjectType.Resource, resource.Id),
                        $"La valeur effective de « {value.Name} » pour « {resource.LogicalName} » ne respecte pas le catalogue : {reason}.",
                        $"The effective value of '{value.Name}' for '{resource.LogicalName}' violates the catalog: {reason}.",
                        target.Target.Id));
                }
            }
        }

        return findings;
    }

    private static bool ShouldReportUnresolvedCondition(EffectiveValueConditionIssue issue) =>
        issue.HasNonDefaultValue || string.Equals(issue.PropertyName, ExposureProperty, StringComparison.Ordinal);

    private static string? GetConstraintFailure(System.Text.Json.JsonElement value, System.Text.Json.JsonElement definition)
    {
        if (definition.TryGetProperty("values", out var allowedValues)
            && allowedValues.ValueKind == System.Text.Json.JsonValueKind.Array
            && !allowedValues.EnumerateArray().Any(candidate => SameScalar(candidate, value)))
        {
            return "la valeur n’est pas dans la liste autorisée";
        }

        if (value.ValueKind == System.Text.Json.JsonValueKind.Number
            && value.TryGetDouble(out var number))
        {
            if (definition.TryGetProperty("minimum", out var minimum) && minimum.TryGetDouble(out var minimumValue) && number < minimumValue)
            {
                return $"la valeur est inférieure au minimum {minimumValue.ToString(CultureInfo.InvariantCulture)}";
            }

            if (definition.TryGetProperty("maximum", out var maximum) && maximum.TryGetDouble(out var maximumValue) && number > maximumValue)
            {
                return $"la valeur dépasse le maximum {maximumValue.ToString(CultureInfo.InvariantCulture)}";
            }
        }

        if (value.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var text = value.GetString() ?? string.Empty;
            if (definition.TryGetProperty("minLength", out var minimumLength) && minimumLength.TryGetInt32(out var minimumLengthValue)
                && text.Length < minimumLengthValue)
            {
                return $"la longueur est inférieure à {minimumLengthValue}";
            }

            if (definition.TryGetProperty("maxLength", out var maximumLength) && maximumLength.TryGetInt32(out var maximumLengthValue)
                && text.Length > maximumLengthValue)
            {
                return $"la longueur dépasse {maximumLengthValue}";
            }

            if (definition.TryGetProperty("pattern", out var pattern) && pattern.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                try
                {
                    if (!Regex.IsMatch(text, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                    {
                        return "le format ne correspond pas au motif du catalogue";
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    return "le motif du catalogue ne peut pas être évalué dans le délai imparti";
                }
                catch (ArgumentException)
                {
                    return "le motif du catalogue est invalide";
                }
            }
        }

        if (value.ValueKind == System.Text.Json.JsonValueKind.Array
            && definition.TryGetProperty("maxItems", out var maximumItems)
            && maximumItems.TryGetInt32(out var maximumItemsValue)
            && value.GetArrayLength() > maximumItemsValue)
        {
            return $"le tableau contient plus de {maximumItemsValue} éléments";
        }

        return null;
    }

    private static bool SameScalar(System.Text.Json.JsonElement left, System.Text.Json.JsonElement right) =>
        left.ValueKind == right.ValueKind
            ? left.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
                System.Text.Json.JsonValueKind.Number => left.TryGetDouble(out var leftNumber)
                    && right.TryGetDouble(out var rightNumber)
                    && leftNumber.Equals(rightNumber),
                System.Text.Json.JsonValueKind.True => right.ValueKind == System.Text.Json.JsonValueKind.True,
                System.Text.Json.JsonValueKind.False => right.ValueKind == System.Text.Json.JsonValueKind.False,
                System.Text.Json.JsonValueKind.Null => right.ValueKind == System.Text.Json.JsonValueKind.Null,
                _ => string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal)
            }
            : left.ValueKind is System.Text.Json.JsonValueKind.String && right.ValueKind == System.Text.Json.JsonValueKind.String
                && string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
}
