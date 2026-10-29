using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class AppSettingSecretOwnerRule : IValidationRule
{
    public string Code => "VAL-PAR-SECRET-PROPRIETAIRE";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var settings = context.Snapshot.AppSettings
            .Where(setting => setting.SourceKind == AppSettingSourceKind.KeyVaultSecret
                && setting.KeyVaultResourceId is not null
                && !string.IsNullOrWhiteSpace(setting.SecretName))
            .ToArray();
        var owners = settings.GroupBy(setting => (VaultId: setting.KeyVaultResourceId!.Value, Name: setting.SecretName!), SecretKeyComparer.Instance)
            .Where(group => group.Select(setting => setting.ComponentId).Distinct().Count() > 1)
            .ToArray();
        return owners.SelectMany(group => group.Select(setting => context.Finding(
            Code,
            Severity,
            new ObjectRef(ObjectType.AppSetting, setting.Id),
            $"Le secret « {setting.SecretName} » du coffre est alimenté par plusieurs composants.",
            $"Key Vault secret '{setting.SecretName}' is written by more than one component.")))
            .ToArray();
    }

    private sealed class SecretKeyComparer : IEqualityComparer<(Guid VaultId, string Name)>
    {
        public static SecretKeyComparer Instance { get; } = new();

        public bool Equals((Guid VaultId, string Name) left, (Guid VaultId, string Name) right) =>
            left.VaultId == right.VaultId && string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((Guid VaultId, string Name) value) =>
            HashCode.Combine(value.VaultId, StringComparer.OrdinalIgnoreCase.GetHashCode(value.Name));
    }
}
