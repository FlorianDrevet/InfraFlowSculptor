using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Naming;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class NamingEmptyRule : IValidationRule
{
    public string Code => "VAL-NOM-VIDE";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Naming.Issues
        .Where(issue => issue.Code == Code)
        .Select(issue => context.Finding(
            Code,
            Severity,
            Subject(issue, context),
            issue.Message,
            $"The generated name for '{issue.ResourceId?.ToString("D") ?? issue.ResourceGroupId?.ToString("D")}' is empty after sanitization.",
            issue.TargetId))
        .ToArray();

    private static ObjectRef Subject(NamingIssue issue, ValidationContext context) => issue.ResourceId is { } resourceId
        ? new ObjectRef(ObjectType.Resource, resourceId)
        : issue.ResourceGroupId is { } groupId
            ? new ObjectRef(ObjectType.ResourceGroup, groupId)
            : new ObjectRef(ObjectType.Project, context.Snapshot.Project.Id);
}
