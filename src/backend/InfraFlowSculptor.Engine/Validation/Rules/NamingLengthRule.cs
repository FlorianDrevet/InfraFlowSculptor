using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Naming;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class NamingLengthRule : IValidationRule
{
    public string Code => "VAL-NOM-LONGUEUR";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Naming.Issues
        .Where(issue => issue.Code == Code)
        .Select(issue => context.Finding(
            Code,
            Severity,
            GetSubject(issue, context),
            issue.Message,
            $"The generated Azure name '{issue.Value}' exceeds the naming limits for this resource.",
            issue.TargetId,
            fixCode: Code))
        .ToArray();

    private static ObjectRef GetSubject(NamingIssue issue, ValidationContext context) => issue.ResourceId is { } resourceId
        ? new ObjectRef(ObjectType.Resource, resourceId)
        : issue.ResourceGroupId is { } groupId
            ? new ObjectRef(ObjectType.ResourceGroup, groupId)
            : new ObjectRef(ObjectType.Project, context.Snapshot.Project.Id);
}
