using System.Text.Json;
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
        var failure = CatalogPropertyValueValidator.Validate(value, definition);
        if (failure?.Kind == CatalogPropertyValueFailureKind.Type)
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

        if (failure is null)
        {
            return Result.Success;
        }

        return failure.Kind switch
        {
            CatalogPropertyValueFailureKind.Range => Error.Validation("RESOURCE_PROPERTY_RANGE", $"Property '{name}' is outside its descriptor range."),
            CatalogPropertyValueFailureKind.Length => Error.Validation("RESOURCE_PROPERTY_LENGTH", $"Property '{name}' is outside its descriptor length limits."),
            CatalogPropertyValueFailureKind.Pattern => Error.Validation("RESOURCE_PROPERTY_PATTERN", $"Property '{name}' does not match its descriptor pattern."),
            CatalogPropertyValueFailureKind.Format => Error.Validation("RESOURCE_PROPERTY_FORMAT", $"Property '{name}' does not match its descriptor format."),
            CatalogPropertyValueFailureKind.Deprecated => Error.Validation("RESOURCE_PROPERTY_DEPRECATED", $"Property '{name}' uses a deprecated value."),
            CatalogPropertyValueFailureKind.Forbidden => Error.Validation("RESOURCE_PROPERTY_VALUE_FORBIDDEN", $"Property '{name}' uses a value forbidden by its descriptor."),
            CatalogPropertyValueFailureKind.EnumValue => Error.Validation("RESOURCE_PROPERTY_ENUM_INVALID", $"Property '{name}' must use a value declared by its descriptor."),
            CatalogPropertyValueFailureKind.ArrayLength => Error.Validation("RESOURCE_PROPERTY_ARRAY_LENGTH", $"Property '{name}' exceeds its descriptor item limit."),
            CatalogPropertyValueFailureKind.ItemType => Error.Validation("RESOURCE_PROPERTY_ITEM_TYPE_INVALID", $"Property '{name}' contains an item that does not match its descriptor type."),
            CatalogPropertyValueFailureKind.DuplicateItemValue => Error.Validation("RESOURCE_PROPERTY_ARRAY_ITEM_DUPLICATE", $"Property '{name}' contains duplicate values for '{failure.DisplayMemberName}'."),
            CatalogPropertyValueFailureKind.UnknownObjectMember => Error.Validation("RESOURCE_PROPERTY_OBJECT_MEMBER_UNKNOWN", $"Property '{name}' contains an object member that is not declared by its descriptor."),
            CatalogPropertyValueFailureKind.DuplicateObjectMember => Error.Validation("RESOURCE_PROPERTY_OBJECT_MEMBER_DUPLICATE", $"Property '{name}' contains duplicate object member '{failure.DisplayMemberName}'."),
            CatalogPropertyValueFailureKind.RequiredObjectMember => Error.Validation("RESOURCE_PROPERTY_OBJECT_MEMBER_REQUIRED", $"Property '{name}' is missing or has a null value for required member '{failure.DisplayMemberName}'."),
            _ => throw new InvalidOperationException($"Unknown catalog property validation failure: {failure.Kind}.")
        };
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

}
