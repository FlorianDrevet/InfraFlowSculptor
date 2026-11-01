using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Targets;
using InfraFlowSculptor.Engine.Values;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ResourceRequiredPropertyRule : IValidationRule
{
    public string Code => "VAL-RES-REQUIS";

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

            var requiredProperties = descriptor.Definition.GetProperty("properties").EnumerateArray()
                .Where(property => property.TryGetProperty("condition", out var condition)
                    && condition.ValueKind == System.Text.Json.JsonValueKind.String
                    && condition.GetString()!.StartsWith("requiredWhen:", StringComparison.Ordinal))
                .ToDictionary(property => property.GetProperty("name").GetString()!, StringComparer.Ordinal);
            if (requiredProperties.Count == 0)
            {
                continue;
            }

            foreach (var target in TargetResolver.ResolveResourceTargets(context.Snapshot, component, resource)
                         .Where(value => value.IsPresent))
            {
                var effective = EffectiveValueResolver.ResolveDetailed(resource, target.Target.Id, descriptor.Definition).Values
                    .Where(value => value.Provenance == EffectiveValueProvenance.Absent
                        && requiredProperties.ContainsKey(value.Name));
                foreach (var property in effective)
                {
                    findings.Add(context.Finding(
                        Code,
                        Severity,
                        new ObjectRef(ObjectType.Resource, resource.Id),
                        $"La propriété « {property.Name} » de « {resource.LogicalName} » est obligatoire dans « {target.Target.Code} » et n’a aucune valeur.",
                        $"Property '{property.Name}' on '{resource.LogicalName}' is required in '{target.Target.Code}' and has no value.",
                        target.Target.Id));
                }
            }
        }

        return findings;
    }
}
