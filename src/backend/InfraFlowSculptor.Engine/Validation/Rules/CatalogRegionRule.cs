using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Targets;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class CatalogRegionRule : IValidationRule
{
    public string Code => "VAL-CAT-REGION";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var supportedRegions = context.Catalog.Regions.Select(region => region.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var findings = new List<ValidationFinding>();
        foreach (var (component, resource) in context.Resources)
        {
            if (!context.ResourceTypes.ContainsKey(resource.Type))
            {
                continue;
            }

            foreach (var target in TargetResolver.ResolveResourceTargets(context.Snapshot, component, resource)
                         .Where(value => value.IsPresent && !supportedRegions.Contains(value.Region)))
            {
                findings.Add(context.Finding(
                    Code,
                    Severity,
                    new ObjectRef(ObjectType.Resource, resource.Id),
                    $"La région « {target.Region} » de « {resource.LogicalName} » n’existe pas dans le catalogue épinglé.",
                    $"Region '{target.Region}' for '{resource.LogicalName}' is not in the pinned catalog.",
                    target.Target.Id));
            }
        }

        return findings;
    }
}
