using System.Text.Json;

namespace InfraFlowSculptor.Engine.Validation.Constraints;

public sealed class AllowedTuplesResourceConstraintEvaluator : IResourceConstraintEvaluator
{
    public string Key => "allowedTuples";

    public IReadOnlyList<string> GetReferencedProperties(JsonElement constraint)
    {
        var properties = constraint.GetProperty("properties").EnumerateArray()
            .Select(property => property.GetString()!)
            .ToList();
        if (constraint.TryGetProperty("when", out var condition))
        {
            properties.Add(condition.GetProperty("property").GetString()!);
        }

        return properties;
    }

    public ResourceConstraintFailure? GetFailure(JsonElement constraint, IReadOnlyDictionary<string, JsonElement> values)
    {
        if (constraint.TryGetProperty("when", out var condition))
        {
            var conditionName = condition.GetProperty("property").GetString()!;
            var expectedValue = condition.GetProperty("equals");
            if (!values.TryGetValue(conditionName, out var actualValue)
                || !JsonElement.DeepEquals(actualValue, expectedValue))
            {
                return null;
            }
        }

        var propertyNames = constraint.GetProperty("properties").EnumerateArray()
            .Select(property => property.GetString()!)
            .ToArray();
        if (propertyNames.Any(name => !values.ContainsKey(name)))
        {
            return null;
        }

        var allowedTuples = constraint.GetProperty("tuples").EnumerateArray();
        foreach (var allowedTuple in allowedTuples)
        {
            if (allowedTuple.ValueKind != JsonValueKind.Array
                || allowedTuple.GetArrayLength() != propertyNames.Length)
            {
                return new ResourceConstraintFailure(
                    "le descripteur contient un tuple autorisé de longueur incorrecte",
                    "the descriptor contains an allowed tuple with an incorrect length");
            }

            var matches = true;
            var expectedValues = allowedTuple.EnumerateArray().ToArray();
            for (var index = 0; index < propertyNames.Length; index++)
            {
                if (!JsonElement.DeepEquals(values[propertyNames[index]], expectedValues[index]))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return null;
            }
        }

        return new ResourceConstraintFailure(
            $"les valeurs de {string.Join(" / ", propertyNames.Select(name => $"« {name} »"))} ne forment aucun tuple autorisé",
            $"the values of {string.Join(" / ", propertyNames.Select(name => $"'{name}'"))} do not form an allowed tuple");
    }
}
