using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class AppSettingConflictRule : IValidationRule
{
    public string Code => "VAL-PAR-SECRET-CONFLIT";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Snapshot.AppSettings
        .GroupBy(setting => (setting.ApplicationResourceId, setting.Name), AppSettingKeyComparer.Instance)
        .Where(group => group.Count() > 1)
        .SelectMany(group => group.Select(setting => context.Finding(
            Code,
            Severity,
            new ObjectRef(ObjectType.AppSetting, setting.Id),
            $"Le paramètre « {setting.Name} » de l’application est alimenté plusieurs fois.",
            $"Application setting '{setting.Name}' has more than one configured source.",
            relatedLinkIds: [])))
        .ToArray();

    private sealed class AppSettingKeyComparer : IEqualityComparer<(Guid ApplicationResourceId, string Name)>
    {
        public static AppSettingKeyComparer Instance { get; } = new();

        public bool Equals((Guid ApplicationResourceId, string Name) left, (Guid ApplicationResourceId, string Name) right) =>
            left.ApplicationResourceId == right.ApplicationResourceId
            && string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((Guid ApplicationResourceId, string Name) value) =>
            HashCode.Combine(value.ApplicationResourceId, StringComparer.OrdinalIgnoreCase.GetHashCode(value.Name));
    }
}
