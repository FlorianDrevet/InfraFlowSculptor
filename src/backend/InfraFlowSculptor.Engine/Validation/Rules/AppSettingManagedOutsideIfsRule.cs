using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class AppSettingManagedOutsideIfsRule : IValidationRule
{
    public string Code => "VAL-PAR-HORS-IFS";

    public FindingSeverity Severity => FindingSeverity.Info;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Snapshot.AppSettings
        .Where(setting => setting.SecretFeedKind == AppSettingSecretFeedKind.ManagedOutsideIfs)
        .Select(setting => context.Finding(
            Code,
            Severity,
            new ObjectRef(ObjectType.AppSetting, setting.Id),
            $"Le secret « {setting.Name} » est géré hors d’IFS et doit exister avant le déploiement.",
            $"Secret '{setting.Name}' is managed outside IFS and must exist before deployment."))
        .ToArray();
}
