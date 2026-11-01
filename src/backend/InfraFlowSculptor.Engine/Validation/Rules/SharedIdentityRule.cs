using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class SharedIdentityRule : IValidationRule
{
    public string Code => "VAL-LIA-IDENTITE-PARTAGEE";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var issues = context.Ordering.Wiring.Issues.Where(issue => issue.Code == Code).ToArray();
        return issues.Select(issue =>
        {
            var subjectId = issue.ResourceId ?? context.Snapshot.Project.Id;
            var linkIds = issue.ResourceId is { } resourceId
                ? context.Snapshot.Links
                    .Where(link => link.SourceResourceId == resourceId || link.IdentityResourceId == resourceId)
                    .Select(link => link.Id)
                    .Order()
                    .ToArray()
                : [];
            var subjectType = issue.ResourceId is null ? ObjectType.Project : ObjectType.Resource;
            return context.Finding(
                Code,
                Severity,
                new ObjectRef(subjectType, subjectId),
                "Une identité affectée est utilisée par une ressource d’un autre composant.",
                "A user-assigned identity is used by a resource in another component.",
                relatedLinkIds: linkIds);
        }).ToArray();
    }
}
