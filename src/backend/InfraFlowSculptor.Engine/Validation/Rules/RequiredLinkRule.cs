using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class RequiredLinkRule : IValidationRule
{
    public string Code => "VAL-LIA-OBLIGATOIRE";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var findings = new List<ValidationFinding>();
        foreach (var issue in context.Ordering.Wiring.Issues.Where(issue => issue.Code == Code))
        {
            if (issue.ResourceId is not { } resourceId
                || issue.Kind is not { } kind
                || !context.TryGetResource(resourceId, out _, out var resource))
            {
                continue;
            }

            var handler = context.LinkKinds.Get(kind);
            var canOfferFix = context.ResourceTypes.TryGetValue(resource.Type, out var sourceType)
                && handler.Form.Fields.All(field => !field.Required)
                && context.Resources.Count(candidate => candidate.Resource.Id != resourceId
                    && context.ResourceTypes.TryGetValue(candidate.Resource.Type, out var targetType)
                    && handler.Supports(sourceType, targetType)) == 1;
            var fixCode = canOfferFix ? Code : null;
            findings.Add(context.Finding(
                Code,
                Severity,
                new ObjectRef(ObjectType.Resource, resourceId),
                $"La ressource « {resource.LogicalName} » n’a pas la liaison obligatoire {kind}.",
                $"Resource '{resource.LogicalName}' is missing its required {kind} link.",
                fixCode: fixCode,
                detailKey: kind.ToString()));
        }

        return findings;
    }
}
