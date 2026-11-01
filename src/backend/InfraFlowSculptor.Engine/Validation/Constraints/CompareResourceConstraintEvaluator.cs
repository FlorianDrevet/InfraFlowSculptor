using System.Text.Json;

namespace InfraFlowSculptor.Engine.Validation.Constraints;

public sealed class CompareResourceConstraintEvaluator : IResourceConstraintEvaluator
{
    public string Key => "compare";

    public IReadOnlyList<string> GetReferencedProperties(JsonElement constraint) =>
    [
        constraint.GetProperty("left").GetString()!,
        constraint.GetProperty("right").GetString()!
    ];

    public ResourceConstraintFailure? GetFailure(JsonElement constraint, IReadOnlyDictionary<string, JsonElement> values)
    {
        var operation = constraint.GetProperty("operator").GetString();
        if (!string.Equals(operation, "lessThanOrEqual", StringComparison.Ordinal))
        {
            return new ResourceConstraintFailure(
                $"l’opérateur « {operation} » n’est pas pris en charge",
                $"operator '{operation}' is not supported");
        }

        var leftName = constraint.GetProperty("left").GetString()!;
        var rightName = constraint.GetProperty("right").GetString()!;
        if (!values.TryGetValue(leftName, out var left) || !values.TryGetValue(rightName, out var right))
        {
            return null;
        }

        if (left.ValueKind != JsonValueKind.Number
            || right.ValueKind != JsonValueKind.Number
            || !left.TryGetDecimal(out var leftNumber)
            || !right.TryGetDecimal(out var rightNumber))
        {
            return null;
        }

        return leftNumber <= rightNumber
            ? null
            : new ResourceConstraintFailure(
                $"« {leftName} » doit être inférieur ou égal à « {rightName} »",
                $"'{leftName}' must be less than or equal to '{rightName}'");
    }
}
