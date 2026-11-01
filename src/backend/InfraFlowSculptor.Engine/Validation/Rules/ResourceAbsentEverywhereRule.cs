using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Targets;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ResourceAbsentEverywhereRule : IValidationRule
{
    public string Code => "VAL-RES-ABSENTE-PARTOUT";

    public FindingSeverity Severity => FindingSeverity.Warning;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Resources
        .Where(item => !item.Resource.IsExisting)
        .Where(item =>
        {
            var targets = TargetResolver.ResolveResourceTargets(context.Snapshot, item.Component, item.Resource);
            return targets.Count > 0 && targets.All(target => !target.IsPresent);
        })
        .Select(item => context.Finding(
            Code,
            Severity,
            new ObjectRef(ObjectType.Resource, item.Resource.Id),
            $"La ressource « {item.Resource.LogicalName} » est absente de tous les environnements ciblés.",
            $"Resource '{item.Resource.LogicalName}' is absent from every targeted environment."))
        .ToArray();
}
