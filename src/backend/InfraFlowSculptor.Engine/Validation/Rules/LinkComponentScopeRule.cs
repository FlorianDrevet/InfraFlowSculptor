using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class LinkComponentScopeRule : IValidationRule
{
    public string Code => "VAL-LIA-COMPOSANT";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var findings = new List<ValidationFinding>();
        var issuesByResource = context.Ordering.Wiring.Issues
            .Where(issue => issue.Code == Code && issue.ResourceId is not null)
            .GroupBy(issue => issue.ResourceId);
        foreach (var group in issuesByResource)
        {
            if (group.Key is not { } resourceId
                || !context.TryGetResource(resourceId, out _, out _))
            {
                continue;
            }

            var linkIds = group
                .Where(issue => issue.LinkId is not null)
                .Select(issue => issue.LinkId!.Value)
                .Distinct()
                .Order()
                .ToArray();
            findings.Add(context.Finding(
                Code,
                Severity,
                new ObjectRef(ObjectType.Resource, resourceId),
                "Les ressources liées doivent appartenir au même composant.",
                "Linked resources must belong to the same component.",
                relatedLinkIds: linkIds));
        }

        return findings;
    }
}
