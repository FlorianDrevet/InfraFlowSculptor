using ErrorOr;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Application.Naming;

public static class NamingOverrideTypeValidator
{
    public static ErrorOr<Success> Validate(
        IReadOnlyList<NamingOverrideValue> overrides,
        CatalogVersion catalog)
    {
        foreach (var value in overrides)
        {
            var type = value.Type?.Trim() ?? string.Empty;
            if (!string.Equals(type, BuiltInNamingRules.ResourceGroupType, StringComparison.OrdinalIgnoreCase)
                && catalog.GetResourceType(type) is null)
            {
                return Error.Validation(
                    "NAMING_RESOURCE_TYPE_UNKNOWN",
                    $"Le type « {type} » n’existe pas dans le catalogue épinglé du projet.");
            }
        }

        return Result.Success;
    }
}
