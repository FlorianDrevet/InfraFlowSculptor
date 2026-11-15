using System.Security.Cryptography;
using System.Text;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Plan;

public static class InstallationChecklistBuilder
{
    public const string PlatformPrerequisitesSection = "platformPrerequisites";
    public const string AzureScriptSection = "azureScript";
    public const string ScriptFollowUpSection = "scriptFollowUp";
    public const string PlatformSetupSection = "platformSetup";
    public const string PipelineSecretsSection = "pipelineSecrets";
    public const string ExternalSecretsSection = "externalSecrets";
    public const string FirstDeploymentSection = "firstDeployment";
    public const string MissingExtensionsSection = "missingExtensions";
    public const string MigrationCutoverSection = "migrationCutover";

    private const string PipelineFile = ".ifs/install/install.pipeline.yml";
    private const string AzureScript = "./.ifs/install/azure-setup.ps1";
    private const string NoItemsKey = "none";

    public static InstallationChecklist Build(DeploymentPlan plan, DeploymentPlan? previousPlan = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var sections = new[]
        {
            BuildPlatformPrerequisites(plan),
            BuildAzureScript(plan),
            BuildScriptFollowUp(),
            BuildPlatformSetup(plan),
            BuildPipelineSecrets(plan),
            BuildExternalSecrets(plan),
            BuildFirstDeployment(plan),
            BuildMissingExtensions(),
            BuildMigrationCutover(plan, previousPlan)
        };

        if (previousPlan is null)
        {
            return new InstallationChecklist(sections);
        }

        var previousChecklist = previousPlan.InstallationChecklist ?? Build(previousPlan);
        var previousItems = previousChecklist.Sections
            .SelectMany(section => section.Items.Select(item => (section.Code, item.Key)))
            .ToHashSet();
        return new InstallationChecklist(sections.Select(section => section with
        {
            Items = section.Items.Select(item => IsDifferenceCandidate(section.Code, item.Key)
                ? item with { IsNewSincePrevious = !previousItems.Contains((section.Code, item.Key)) }
                : item).ToArray()
        }).ToArray());
    }

    private static InstallationChecklistSection BuildPlatformPrerequisites(DeploymentPlan plan)
    {
        var targets = plan.Components.SelectMany(component => component.Targets)
            .Select(target => target.Target.Code)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var items = targets.Length == 0
            ? [Automatic(NoItemsKey, "Aucune cible de déploiement n’est configurée.")]
            : targets.Select(target => ToDo(
                $"target-{target}",
                $"Vérifier les droits du compte de build sur la cible {target}.",
                isNewSincePrevious: false,
                isUserActionable: true)).ToArray();

        return new InstallationChecklistSection(
            PlatformPrerequisitesSection,
            "1. Prérequis Azure DevOps",
            "Permissions du compte de build",
            items);
    }

    private static InstallationChecklistSection BuildAzureScript(DeploymentPlan plan)
    {
        var command = $"{AzureScript} -AzureDevOpsOrganization https://dev.azure.com/<organisation> "
            + $"-AzureDevOpsProject {plan.Project.Code} -TenantId <tenant-id> -WhatIf";
        return new InstallationChecklistSection(
            AzureScriptSection,
            "2. Script Azure",
            "Prévisualiser les opérations avant de créer les ressources",
            [ToDo("run-azure-script", "Prévisualiser puis exécuter le script Azure.", command: command)]);
    }

    private static InstallationChecklistSection BuildScriptFollowUp() => new(
        ScriptFollowUpSection,
        "3. Étapes laissées par le script",
        "Actions qui nécessitent un administrateur",
        [ToDo(
            "complete-script-follow-up",
            "Effectuer les étapes laissées à faire par le script, si le rapport en signale.")]);

    private static InstallationChecklistSection BuildPlatformSetup(DeploymentPlan plan)
    {
        var items = new List<InstallationChecklistItem>
        {
            ToDo("run-install-pipeline", $"Créer puis exécuter le pipeline {PipelineFile} dans Azure DevOps.")
        };
        items.AddRange((plan.Project.Repositories ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(repository => ToDo(
                CreateStableKey("repository", repository),
                $"Vérifier le dépôt de publication {repository}.",
                isNewSincePrevious: false,
                isUserActionable: true)));

        return new InstallationChecklistSection(
            PlatformSetupSection,
            "4. Pipeline d’installation Azure DevOps",
            "Les environnements et pipelines sont créés par le kit",
            items);
    }

    private static InstallationChecklistSection BuildPipelineSecrets(DeploymentPlan plan)
    {
        var secrets = plan.Components.SelectMany(component => component.Targets.Select(target =>
                (component.ComponentCode, Target: target)))
            .SelectMany(entry => entry.Target.ExpectedSecrets.Select(secret =>
            {
                var parameter = entry.Target.AppSettings.FirstOrDefault(setting =>
                    string.Equals(setting.PipelineVariableName, secret.Variable, StringComparison.Ordinal));
                var variableGroup = $"ifs-{plan.Project.Code}-{entry.Target.Target.Code}";
                var parameterText = parameter is null ? string.Empty : $" → paramètre {parameter.Name}";
                return ToDo(
                    CreateStableKey("secret", entry.Target.Target.Code, secret.Variable, secret.Vault, secret.Secret),
                    $"{entry.Target.Target.Code} · {variableGroup} · {secret.Variable}{parameterText}",
                    isNewSincePrevious: false,
                    isUserActionable: true);
            }))
            .OrderBy(item => item.Text, StringComparer.Ordinal)
            .ToArray();
        var items = secrets.Length == 0
            ? [Automatic(NoItemsKey, "Aucun secret de pipeline n’est attendu.")]
            : secrets;

        return new InstallationChecklistSection(
            PipelineSecretsSection,
            "5. Secrets de pipeline",
            "IFS ne voit jamais leur valeur",
            items);
    }

    private static InstallationChecklistSection BuildExternalSecrets(DeploymentPlan plan)
    {
        var secrets = plan.Components.SelectMany(component => component.Targets.Select(target =>
                (component.ComponentCode, Target: target)))
            .SelectMany(entry => entry.Target.AppSettings
                .Where(setting => string.Equals(setting.SecretFeedKind, "managedOutsideIfs", StringComparison.Ordinal)
                    && setting.KeyVault is not null
                    && setting.SecretName is not null)
                .Select(setting => ToDo(
                    CreateStableKey(
                        "external-secret",
                        entry.Target.Target.Code,
                        setting.KeyVault!,
                        setting.SecretName!,
                        setting.Name),
                    $"{entry.Target.Target.Code} · {setting.KeyVault}/{setting.SecretName} → paramètre {setting.Name}",
                    isNewSincePrevious: false,
                    isUserActionable: true)))
            .OrderBy(item => item.Text, StringComparer.Ordinal)
            .ToArray();
        var items = secrets.Length == 0
            ? [Automatic(NoItemsKey, "Aucun secret géré hors IFS n’est attendu.")]
            : secrets;

        return new InstallationChecklistSection(
            ExternalSecretsSection,
            "6. Secrets gérés hors IFS",
            "Vérifier leur présence dans les coffres de clés",
            items);
    }

    private static InstallationChecklistSection BuildFirstDeployment(DeploymentPlan plan)
    {
        var items = new List<InstallationChecklistItem>();
        foreach (var component in plan.Components)
        {
            foreach (var target in component.Targets)
            {
                var dependencyOrder = component.DependsOn.Count == 0
                    ? component.ComponentCode
                    : $"{string.Join(" → ", component.DependsOn)} → {component.ComponentCode}";
                items.Add(ToDo(
                    CreateStableKey("component", component.ComponentCode, target.Target.Code),
                    $"{target.Target.Code} · {dependencyOrder}",
                    isNewSincePrevious: false,
                    isUserActionable: false));

                foreach (var application in component.Applications
                    .SelectMany(application => application.Targets
                        .Where(applicationTarget => applicationTarget.TargetCode == target.Target.Code)
                        .Select(_ => application)))
                {
                    items.Add(ToDo(
                        CreateStableKey("application", component.ComponentCode, application.Name, target.Target.Code),
                        $"{target.Target.Code} · application {application.Name}",
                        isNewSincePrevious: false,
                        isUserActionable: false));
                }
            }
        }

        if (items.Count == 0)
        {
            items.Add(Automatic(NoItemsKey, "Aucun premier déploiement n’est prévu."));
        }

        return new InstallationChecklistSection(
            FirstDeploymentSection,
            "7. Premier déploiement",
            "Coché automatiquement après le premier succès",
            items);
    }

    private static InstallationChecklistSection BuildMissingExtensions() => new(
        MissingExtensionsSection,
        "8. Points d’extension",
        "Vérifier les extensions référencées mais absentes",
        [ToDo("check-missing-extensions", "Installer ou remplacer les points d’extension manquants.")]);

    private static InstallationChecklistSection BuildMigrationCutover(
        DeploymentPlan plan,
        DeploymentPlan? previousPlan)
    {
        var languageChanged = previousPlan is not null
            && !string.Equals(plan.Project.InfrastructureLanguage, previousPlan.Project.InfrastructureLanguage, StringComparison.Ordinal);
        var platformChanged = previousPlan is not null
            && !string.Equals(plan.Project.CiPlatform, previousPlan.Project.CiPlatform, StringComparison.Ordinal);
        var migrationRequired = languageChanged || platformChanged;
        var item = migrationRequired
            ? ToDo("migration-cutover", "Planifier la bascule, valider les objets orphelins puis retirer les anciens accès.")
            : Automatic("no-migration", "Aucune migration de langage ou de plateforme n’est prévue pour cette révision.");

        return new InstallationChecklistSection(
            MigrationCutoverSection,
            "9. Bascule après une migration",
            "Langage d’infrastructure ou plateforme de CI",
            [item]);
    }

    private static InstallationChecklistItem ToDo(
        string key,
        string text,
        bool isNewSincePrevious = false,
        bool isUserActionable = true,
        string? command = null) =>
        new(key, text, InstallationChecklistItemState.ToDo, isUserActionable, command, isNewSincePrevious);

    private static InstallationChecklistItem Automatic(string key, string text) =>
        new(key, text, InstallationChecklistItemState.Automatic, IsUserActionable: false);

    private static string CreateStableKey(string prefix, params string[] values)
    {
        var value = string.Join('\0', values);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        return $"{prefix}-{hash[..24]}";
    }

    private static bool IsDifferenceCandidate(string sectionCode, string key) =>
        sectionCode switch
        {
            PlatformPrerequisitesSection => key.StartsWith("target-", StringComparison.Ordinal),
            PlatformSetupSection => key.StartsWith("repository-", StringComparison.Ordinal),
            PipelineSecretsSection => key.StartsWith("secret-", StringComparison.Ordinal),
            ExternalSecretsSection => key.StartsWith("external-secret-", StringComparison.Ordinal),
            FirstDeploymentSection => key.StartsWith("component-", StringComparison.Ordinal)
                || key.StartsWith("application-", StringComparison.Ordinal),
            _ => false
        };
}
