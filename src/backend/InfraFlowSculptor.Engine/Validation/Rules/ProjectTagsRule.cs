using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Tags;
using InfraFlowSculptor.Engine.Targets;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ProjectTagsRule : IValidationRule
{
    public string Code => "VAL-PRJ-TAGS";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var findings = new List<ValidationFinding>();
        foreach (var (component, resource) in context.Resources)
        {
            var group = component.ResourceGroups.SingleOrDefault(value => value.Id == resource.ResourceGroupId);
            if (group is null)
            {
                continue;
            }

            foreach (var target in TargetResolver.ResolveResourceTargets(context.Snapshot, component, resource)
                         .Where(value => value.IsPresent))
            {
                var effectiveTags = TagResolver.Resolve(context.Snapshot, component, group, resource, target);
                if (effectiveTags.Count > 50)
                {
                    findings.Add(context.Finding(
                        Code,
                        Severity,
                        new ObjectRef(ObjectType.Resource, resource.Id),
                        $"La ressource « {resource.LogicalName} » a {effectiveTags.Count} tags effectifs dans « {target.Target.Code} »; la limite est 50.",
                        $"Resource '{resource.LogicalName}' has {effectiveTags.Count} effective tags in '{target.Target.Code}'; the limit is 50.",
                        target.Target.Id));
                }
            }
        }

        return findings;
    }
}
