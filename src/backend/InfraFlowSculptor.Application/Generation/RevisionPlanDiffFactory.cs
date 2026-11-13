using System.Text.Json;
using InfraFlowSculptor.Domain.RevisionAggregate;

namespace InfraFlowSculptor.Application.Generation;

public static class RevisionPlanDiffFactory
{
    private const string PlanAdded = "PlanAdded";
    private const string PlanChanged = "PlanChanged";
    private const string PlanRemoved = "PlanRemoved";

    public static IReadOnlyList<RevisionChange> Create(string? previousPlanJson, string currentPlanJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPlanJson);
        if (string.IsNullOrWhiteSpace(previousPlanJson))
        {
            return [];
        }

        using var previous = JsonDocument.Parse(previousPlanJson);
        using var current = JsonDocument.Parse(currentPlanJson);
        var changes = new List<RevisionChange>();
        Compare(string.Empty, previous.RootElement, current.RootElement, changes);
        return changes;
    }

    private static void Compare(
        string path,
        JsonElement previous,
        JsonElement current,
        ICollection<RevisionChange> changes)
    {
        if (previous.ValueKind != current.ValueKind)
        {
            changes.Add(CreateChange(PlanChanged, path));
            return;
        }

        switch (current.ValueKind)
        {
            case JsonValueKind.Object:
                CompareObjects(path, previous, current, changes);
                break;
            case JsonValueKind.Array:
                CompareArrays(path, previous, current, changes);
                break;
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                if (!string.Equals(previous.GetRawText(), current.GetRawText(), StringComparison.Ordinal))
                {
                    changes.Add(CreateChange(PlanChanged, path));
                }

                break;
            default:
                throw new InvalidDataException("The deployment plan contains an unsupported JSON token.");
        }
    }

    private static void CompareObjects(
        string path,
        JsonElement previous,
        JsonElement current,
        ICollection<RevisionChange> changes)
    {
        var previousProperties = previous.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
        var currentProperties = current.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);

        foreach (var propertyName in previousProperties.Keys
                     .Union(currentProperties.Keys, StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            var propertyPath = AppendPath(path, propertyName);
            var hasPrevious = previousProperties.TryGetValue(propertyName, out var previousValue);
            var hasCurrent = currentProperties.TryGetValue(propertyName, out var currentValue);
            if (!hasPrevious)
            {
                changes.Add(CreateChange(PlanAdded, propertyPath));
            }
            else if (!hasCurrent)
            {
                changes.Add(CreateChange(PlanRemoved, propertyPath));
            }
            else
            {
                Compare(propertyPath, previousValue, currentValue, changes);
            }
        }
    }

    private static void CompareArrays(
        string path,
        JsonElement previous,
        JsonElement current,
        ICollection<RevisionChange> changes)
    {
        var previousItems = previous.EnumerateArray().ToArray();
        var currentItems = current.EnumerateArray().ToArray();
        var count = Math.Max(previousItems.Length, currentItems.Length);
        for (var index = 0; index < count; index++)
        {
            var itemPath = AppendPath(path, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (index >= previousItems.Length)
            {
                changes.Add(CreateChange(PlanAdded, itemPath));
            }
            else if (index >= currentItems.Length)
            {
                changes.Add(CreateChange(PlanRemoved, itemPath));
            }
            else
            {
                Compare(itemPath, previousItems[index], currentItems[index], changes);
            }
        }
    }

    private static string AppendPath(string path, string segment) =>
        $"{path}/{segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)}";

    private static RevisionChange CreateChange(string category, string path) => new(
        category,
        string.IsNullOrEmpty(path) ? "/" : path,
        "Plan de déploiement modifié",
        "Deployment plan changed");
}
