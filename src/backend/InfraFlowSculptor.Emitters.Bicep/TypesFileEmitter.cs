using System.Text.Json;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.Bicep;

internal static class TypesFileEmitter
{
    public static GeneratedFile Emit(DeploymentPlan plan, ComponentPlan component, CatalogVersion catalog)
    {
        var writer = new BicepWriter();
        WriteHeader(writer, plan.Project.Code);

        WriteTargetConfig(writer, component);
        WriteResourceGroupConfig(writer);
        WriteResourceGroupsConfig(writer, component);

        var externalTypes = GetExternalTypes(component, catalog);
        if (externalTypes.Length > 0)
        {
            WriteResourceReferenceConfig(writer);
            if (externalTypes.Any(item => item.HasParent))
            {
                WriteSqlDatabaseReferenceConfig(writer);
            }

            if (externalTypes.Length > 1)
            {
                WriteExternalResourcesConfig(writer, component, externalTypes);
            }
        }

        var configs = GetComponentConfigs(component, catalog);
        if (configs.SelectMany(item => item.Fields)
            .Any(field => field.Type == "diagnosticSettingConfig[]"))
        {
            var hasMetricOnlySettings = configs.SelectMany(item => item.Fields)
                .Any(field => field.Type == "metricDiagnosticSettingConfig[]");
            WriteDiagnosticSettingConfig(writer, hasMetricOnlySettings);
        }

        if (configs.SelectMany(item => item.Fields)
            .Any(field => field.Type == "metricDiagnosticSettingConfig[]"))
        {
            WriteMetricDiagnosticSettingConfig(writer);
        }

        foreach (var config in configs)
        {
            WriteType(writer, config.TypeName, config.Description, config.Fields);
        }

        return new GeneratedFile($"{component.ComponentCode}/infra/types.bicep", writer.ToString().TrimEnd('\n') + "\n");
    }

    private static void WriteTargetConfig(BicepWriter writer, ComponentPlan component)
    {
        var single = component.Mode == ComponentDeploymentMode.Single;
        WriteType(writer, "targetConfig", $"Cible Azure du déploiement du composant {component.ComponentCode}.",
        [
            new("code", "string", single
                ? "Code de la cible partagée, ici shared."
                : "Code de la cible, par exemple dev ou prd."),
            new("subscriptionId", "string", "Identifiant de l’abonnement Azure cible."),
            new("location", "string", "Région Azure des ressources de cette cible.")
        ]);
    }

    private static void WriteResourceGroupConfig(BicepWriter writer) => WriteType(
        writer,
        "resourceGroupConfig",
        "Configuration du groupe de ressources du composant.",
        [
            new("name", "string", "Nom Azure explicite du groupe de ressources."),
            new("location", "string", "Région Azure du groupe de ressources."),
            new("tags", "object", "Tags effectifs du groupe de ressources.")
        ]);

    private static void WriteResourceGroupsConfig(BicepWriter writer, ComponentPlan component) => WriteType(
        writer,
        "resourceGroupsConfig",
        $"Groupes de ressources créés par le composant {component.ComponentCode}.",
        component.Targets.SelectMany(target => target.ResourceGroups)
            .DistinctBy(group => group.LogicalName)
            .OrderBy(group => group.LogicalName, StringComparer.Ordinal)
            .Select(group => new BicepField(
                BicepIdentifier.FromLogicalName(group.LogicalName),
                "resourceGroupConfig",
                $"Groupe de ressources {(string.Equals(group.LogicalName, "main", StringComparison.Ordinal) ? "principal" : group.LogicalName)} du composant {component.ComponentCode}."))
            .ToArray());

    private static ExternalType[] GetExternalTypes(ComponentPlan component, CatalogVersion catalog)
    {
        var externalReferences = component.Targets.SelectMany(target => target.ExternalReferences)
            .GroupBy(reference => (reference.ComponentCode, reference.ResourceType, reference.LogicalName))
            .Select(group => group.First())
            .Select(reference =>
            {
                var descriptor = catalog.GetResourceType(reference.ResourceType)
                    ?? throw new InvalidOperationException(
                        $"The pinned catalog has no descriptor for resource type '{reference.ResourceType}'.");
                var existing = BicepCatalogMetadata.TryGetExisting(descriptor)
                    ?? throw new InvalidOperationException(
                        $"Resource type '{descriptor.Type}' has no Bicep existing-resource metadata.");
                var parameterPath = BicepCatalogMetadata.ReadString(existing, "parameterName")
                    ?? throw new InvalidOperationException(
                        $"Resource type '{descriptor.Type}' has no Bicep external parameter name.");
                return new ExternalType(
                    parameterPath.Split('.')[0],
                    BicepCatalogMetadata.ReadString(existing, "description") ?? descriptor.LabelFr,
                    BicepCatalogMetadata.ReadInt32(existing, "order"),
                    BicepCatalogMetadata.ReadString(existing, "parentType") is not null);
            })
            .GroupBy(item => item.Name, StringComparer.Ordinal)
            .Select(group => new ExternalType(
                group.Key,
                group.OrderByDescending(item => item.HasParent).ThenBy(item => item.Order).First().Description,
                group.Min(item => item.Order),
                group.Any(item => item.HasParent)))
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
        return externalReferences;
    }

    private static void WriteResourceReferenceConfig(BicepWriter writer) => WriteType(
        writer,
        "resourceReferenceConfig",
        "Référence explicite à une ressource d’un autre composant.",
        [
            new("subscriptionId", "string", "Identifiant de l’abonnement contenant la ressource existante."),
            new("resourceGroupName", "string", "Nom du groupe de ressources de la ressource existante."),
            new("name", "string", "Nom Azure de la ressource existante.")
        ]);

    private static void WriteSqlDatabaseReferenceConfig(BicepWriter writer) => WriteType(
        writer,
        "sqlDatabaseReferenceConfig",
        "Référence explicite à une base SQL et à son serveur parent.",
        [
            new("server", "resourceReferenceConfig", "Référence du serveur SQL qui contient la base."),
            new("name", "string", "Nom Azure de la base SQL existante.")
        ]);

    private static void WriteExternalResourcesConfig(
        BicepWriter writer,
        ComponentPlan component,
        IReadOnlyList<ExternalType> externalTypes) => WriteType(
        writer,
        "externalResourcesConfig",
        $"Références aux ressources existantes nécessaires à {component.ComponentCode}.",
        externalTypes.Select(item => new BicepField(
                item.Name,
                item.HasParent ? "sqlDatabaseReferenceConfig" : "resourceReferenceConfig",
                item.Description))
            .ToArray());

    private static void WriteDiagnosticSettingConfig(BicepWriter writer, bool complete) => WriteType(
        writer,
        "diagnosticSettingConfig",
        complete
            ? "Paramètres explicites d’une destination de diagnostic complète."
            : "Paramètres explicites d’une destination de diagnostic vers Log Analytics.",
        [
            new("name", "string", "Nom explicite du paramètre de diagnostic."),
            new("logCategoryGroup", "string", "Groupe de catégories de journal envoyé à Log Analytics."),
            new("metricCategory", "string", "Catégorie de métriques envoyée à Log Analytics."),
            new("logAnalyticsDestinationType", "'Dedicated' | 'AzureDiagnostics'", "Format de destination Log Analytics.")
        ]);

    private static void WriteMetricDiagnosticSettingConfig(BicepWriter writer) => WriteType(
        writer,
        "metricDiagnosticSettingConfig",
        "Paramètres explicites d’une destination de diagnostic métrique.",
        [
            new("name", "string", "Nom explicite du paramètre de diagnostic."),
            new("metricCategory", "string", "Catégorie de métriques envoyée à Log Analytics."),
            new("logAnalyticsDestinationType", "'Dedicated' | 'AzureDiagnostics'", "Format de destination Log Analytics.")
        ]);

    private static BicepConfig[] GetComponentConfigs(ComponentPlan component, CatalogVersion catalog) =>
        component.Targets.SelectMany(target => target.Resources)
            .Where(resource => !resource.IsExisting)
            .DistinctBy(resource => resource.Type)
            .Where(resource =>
            {
                var descriptor = catalog.GetResourceType(resource.Type)
                    ?? throw new InvalidOperationException(
                        $"The pinned catalog has no descriptor for resource type '{resource.Type}'.");
                return !BicepCatalogMetadata.IsEmbedded(descriptor);
            })
            .Select(resource =>
            {
                var descriptor = catalog.GetResourceType(resource.Type)
                    ?? throw new InvalidOperationException(
                        $"The pinned catalog has no descriptor for resource type '{resource.Type}'.");
                var definition = BicepCatalogMetadata.TryGetConfig(descriptor)
                    ?? throw new InvalidOperationException(
                        $"Resource type '{descriptor.Type}' has no Bicep configuration metadata.");
                return new BicepConfig(
                    BicepCatalogMetadata.ReadString(definition, "typeName")!,
                    BicepCatalogMetadata.ReadString(definition, "description")!,
                    BicepCatalogMetadata.ReadInt32(definition, "order"),
                    definition.GetProperty("fields").EnumerateArray()
                        .Where(field => BicepCatalogMetadata.ShouldIncludeField(field, component, resource.Type))
                        .Select(field => new BicepField(
                            BicepCatalogMetadata.ReadString(field, "name")!,
                            BicepCatalogMetadata.ReadString(field, "type")!,
                            BicepCatalogMetadata.ReadString(field, "description")!))
                        .ToArray());
            })
            .OrderBy(config => config.Order)
            .ThenBy(config => config.TypeName, StringComparer.Ordinal)
            .ToArray();

    private static void WriteType(BicepWriter writer, string typeName, string description, IReadOnlyList<BicepField> fields)
    {
        writer.Line("@export()");
        writer.Line($"@description({BicepCatalogMetadata.SingleQuoted(description)})");
        writer.BeginBlock($"type {typeName} = {{");
        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            writer.Line($"@description({BicepCatalogMetadata.SingleQuoted(field.Description)})");
            writer.Line($"{field.Name}: {field.Type}");
            if (index < fields.Count - 1)
            {
                writer.BlankLine();
            }
        }

        writer.EndBlock();
        writer.BlankLine();
    }

    private static void WriteHeader(BicepWriter writer, string projectCode)
    {
        writer.Line($"// Généré par InfraFlowSculptor — projet {projectCode}. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.");
        writer.BlankLine();
    }

    private sealed record BicepConfig(string TypeName, string Description, int Order, IReadOnlyList<BicepField> Fields);

    private sealed record BicepField(string Name, string Type, string Description);

    private sealed record ExternalType(string Name, string Description, int Order, bool HasParent);
}
