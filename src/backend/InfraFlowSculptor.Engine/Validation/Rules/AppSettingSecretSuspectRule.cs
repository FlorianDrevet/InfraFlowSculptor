using System.Text.RegularExpressions;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed partial class AppSettingSecretSuspectRule : IValidationRule
{
    public string Code => "VAL-PAR-SECRET-SUSPECT";

    public FindingSeverity Severity => FindingSeverity.Warning;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Snapshot.AppSettings
        .Where(setting => setting.SourceKind == AppSettingSourceKind.Literal && SecretLikeNameRegex().IsMatch(setting.Name))
        .Select(setting => context.Finding(
            Code,
            Severity,
            new ObjectRef(ObjectType.AppSetting, setting.Id),
            $"Le paramètre « {setting.Name} » ressemble à un secret, mais contient une valeur littérale. Utilisez un secret Key Vault alimenté par pipeline.",
            $"Setting '{setting.Name}' looks like a secret but uses a literal value. Use a pipeline-fed Key Vault secret.",
            fixCode: CanConvertToPipelineSecret(context, setting) ? Code : null))
        .ToArray();

    private static bool CanConvertToPipelineSecret(ValidationContext context, AppSettingSnapshot setting)
    {
        var vaults = context.Resources
            .Where(candidate => string.Equals(candidate.Resource.Type, "KeyVault", StringComparison.Ordinal))
            .ToArray();
        if (vaults.Length != 1
            || !context.TryGetResource(setting.ApplicationResourceId, out var applicationComponent, out var application))
        {
            return false;
        }

        if (string.Equals(application.Type, "ContainerApp", StringComparison.Ordinal)
            && application.DefaultIdentityResourceId is null
            && applicationComponent.Resources.Count(resource => string.Equals(resource.Type, "UserAssignedIdentity", StringComparison.Ordinal)) != 1)
        {
            return false;
        }

        var vault = vaults[0];
        if (vault.Resource.IsExisting)
        {
            return true;
        }

        var orderedIds = context.Ordering.OrderedComponents.Select(component => component.Id).ToArray();
        var vaultIndex = Array.IndexOf(orderedIds, vault.Component.Id);
        var applicationIndex = Array.IndexOf(orderedIds, applicationComponent.Id);
        return vault.Component.Id != applicationComponent.Id
            && vaultIndex >= 0
            && applicationIndex >= 0
            && vaultIndex < applicationIndex;
    }

    [GeneratedRegex("(secret|password|passwd|token|api.?key|connection.?string)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretLikeNameRegex();
}
