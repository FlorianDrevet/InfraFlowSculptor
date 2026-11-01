using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Targets;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ExistingResourceIdRule : IValidationRule
{
    public string Code => "VAL-RES-EXISTANTE-ID";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var findings = new List<ValidationFinding>();
        foreach (var (component, resource) in context.Resources.Where(item => item.Resource.IsExisting))
        {
            foreach (var target in TargetResolver.ResolveTargets(context.Snapshot, component))
            {
                if (!resource.Presences.TryGetValue(target.Id, out var isPresent)
                    || !isPresent
                    || resource.ExistingIds.TryGetValue(target.Id, out var id) && !string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                findings.Add(context.Finding(
                    Code,
                    Severity,
                    new ObjectRef(ObjectType.Resource, resource.Id),
                    $"La ressource existante « {resource.LogicalName} » est présente dans « {target.Code} » sans identifiant Azure.",
                    $"Existing resource '{resource.LogicalName}' is present in '{target.Code}' without an Azure resource ID.",
                    target.Id));
            }
        }

        return findings;
    }
}
