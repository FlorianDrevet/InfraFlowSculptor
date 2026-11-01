using System.Text.Json;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.Bicep;

internal static class MainFileEmitter
{
    public static IReadOnlyList<GeneratedFile> EmitHelpers(
        DeploymentPlan plan,
        ComponentPlan component,
        CatalogVersion catalog)
    {
        var localResources = component.Targets.SelectMany(target => target.Resources)
            .Where(resource => !resource.IsExisting)
            .DistinctBy(resource => resource.Type, StringComparer.Ordinal)
            .Select(resource => GetDescriptor(catalog, resource.Type))
            .Where(descriptor => !BicepCatalogMetadata.IsEmbedded(descriptor))
            .ToArray();
        var externalReferences = component.Targets.SelectMany(target => target.ExternalReferences)
            .Select(reference => GetDescriptor(catalog, reference.ResourceType))
            .DistinctBy(descriptor => descriptor.Type, StringComparer.Ordinal)
            .ToArray();
        var localTypes = localResources.Select(descriptor => descriptor.Type).ToHashSet(StringComparer.Ordinal);
        var externalTypes = externalReferences.Select(descriptor => descriptor.Type).ToHashSet(StringComparer.Ordinal);
        var generated = new List<GeneratedFile>();

        foreach (var descriptor in localResources)
        {
            var bicep = BicepCatalogMetadata.GetBicep(descriptor);
            if (!bicep.TryGetProperty("helpers", out var helpers))
            {
                continue;
            }

            foreach (var helper in helpers.EnumerateArray())
            {
                if (!MatchesRequirements(helper, localTypes, externalTypes))
                {
                    continue;
                }

                var header = helper.GetProperty("header").GetString()!;
                var template = ExpandTemplate(helper.GetProperty("template").GetString()!, catalog, string.Empty);
                var content = header == "project"
                    ? $"// Généré par InfraFlowSculptor — projet {plan.Project.Code}. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.\n\n{template}"
                    : $"// Généré par InfraFlowSculptor — {header}\n{template}";
                generated.Add(new GeneratedFile(
                    $"{component.ComponentCode}/infra/{helper.GetProperty("fileName").GetString()}",
                    content.TrimEnd('\n') + "\n"));
            }
        }

        return generated;
    }

    public static GeneratedFile Emit(
        DeploymentPlan plan,
        ComponentPlan component,
        CatalogVersion catalog,
        ModuleSourceStrategyRegistry moduleSources)
    {
        var localResources = component.Targets.SelectMany(target => target.Resources)
            .Where(resource => !resource.IsExisting)
            .DistinctBy(resource => resource.Type, StringComparer.Ordinal)
            .Select(resource => (Resource: resource, Descriptor: GetDescriptor(catalog, resource.Type)))
            .Where(entry => !BicepCatalogMetadata.IsEmbedded(entry.Descriptor))
            .ToArray();
        var externalReferences = component.Targets.SelectMany(target => target.ExternalReferences)
            .GroupBy(reference => (reference.ComponentCode, reference.ResourceType, reference.LogicalName))
            .Select(group => group.First())
            .Select(reference => (Reference: reference, Descriptor: GetDescriptor(catalog, reference.ResourceType)))
            .OrderBy(entry => GetExistingOrder(entry.Descriptor), Comparer<int>.Default)
            .ThenBy(entry => entry.Reference.ComponentCode, StringComparer.Ordinal)
            .ThenBy(entry => entry.Reference.LogicalName, StringComparer.Ordinal)
            .ToArray();
        var externalRoots = externalReferences
            .Select(entry => GetExternalRoot(entry.Descriptor))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var writer = new BicepWriter();
        WriteHeader(writer, plan.Project.Code);
        writer.BlankLine();
        WriteImports(writer, localResources, externalReferences, externalRoots.Length);
        writer.BlankLine();
        writer.Line("targetScope = 'subscription'");
        writer.BlankLine();
        WriteParameters(writer, localResources, externalReferences, externalRoots);
        writer.BlankLine();

        var statements = new List<MainStatement>();
        var localTypes = localResources.Select(item => item.Descriptor.Type).ToHashSet(StringComparer.Ordinal);
        var externalTypes = externalReferences.Select(item => item.Descriptor.Type).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in localResources)
        {
            var bicep = BicepCatalogMetadata.GetBicep(entry.Descriptor);
            if (!bicep.TryGetProperty("mainStatements", out var configuredStatements))
            {
                continue;
            }

            var moduleReference = moduleSources.Get(ModuleSource.PublicAvmRegistry)
                .GetModuleReference(plan, entry.Descriptor);
            var index = 0;
            foreach (var configured in configuredStatements.EnumerateArray())
            {
                if (!MatchesRequirements(configured, localTypes, externalTypes))
                {
                    index++;
                    continue;
                }

                var isRestricted = BicepCatalogMetadata.HasRestrictedExposure(component, entry.Descriptor.Type);
                var templateDefinition = isRestricted
                    && configured.TryGetProperty("restrictedTemplate", out var restrictedTemplate)
                    ? restrictedTemplate
                    : configured.GetProperty("template");
                var template = ExpandTemplate(templateDefinition.GetString()!, catalog, moduleReference);
                statements.Add(new MainStatement(
                    configured.GetProperty("order").GetInt32(),
                    entry.Descriptor.Type,
                    index,
                    BicepCatalogMetadata.ReadString(configured, "sectionComment"),
                    template));
                index++;
            }
        }

        var statementCount = 0;
        foreach (var statement in OrderStatements(statements).Where(statement => statement.Order < 0))
        {
            WriteMainStatement(writer, ref statementCount, statement);
        }

        var groups = component.Targets.SelectMany(target => target.ResourceGroups)
            .DistinctBy(group => group.LogicalName, StringComparer.Ordinal)
            .OrderBy(group => group.LogicalName, StringComparer.Ordinal)
            .ToArray();
        var groupPin = catalog.Pins.NativeResources.TryGetValue("resourceGroup", out var nativeGroup)
            ? nativeGroup
            : throw new InvalidOperationException("The pinned catalog has no native resource group definition.");
        foreach (var group in groups)
        {
            var groupIdentifier = BicepIdentifier.FromLogicalName(group.LogicalName);
            WriteStatement(writer, ref statementCount, $"resource rg_{groupIdentifier} '{groupPin.Type}@{groupPin.ApiVersion}' = {{\n"
                + $"  name: resourceGroups.{groupIdentifier}.name\n"
                + $"  location: resourceGroups.{groupIdentifier}.location\n"
                + $"  tags: resourceGroups.{groupIdentifier}.tags\n"
                + "}");
        }

        foreach (var entry in externalReferences)
        {
            var existing = BicepCatalogMetadata.TryGetExisting(entry.Descriptor)
                ?? throw new InvalidOperationException(
                    $"Resource type '{entry.Descriptor.Type}' has no Bicep existing-resource metadata.");
            var symbol = ResolveExternalSymbol(existing, component.ComponentCode);
            var parameterPath = GetExternalParameterPath(
                BicepCatalogMetadata.ReadString(existing, "parameterName")!,
                externalRoots.Length > 1);
            var scope = $"resourceGroup({parameterPath}.subscriptionId, {parameterPath}.resourceGroupName)";
            var scopeLine = scope.Length <= 110
                ? $"  scope: {scope}"
                : $"  scope: resourceGroup(\n    {parameterPath}.subscriptionId,\n    {parameterPath}.resourceGroupName\n  )";
            var lines = new List<string>
            {
                $"resource {symbol} '{entry.Descriptor.Definition.GetProperty("armType").GetString()}@{BicepCatalogMetadata.ReadString(existing, "apiVersion")}' existing = {{"
            };
            if (BicepCatalogMetadata.ReadString(existing, "parentSymbol") is { } parentSymbol)
            {
                lines.Add($"  parent: {parentSymbol}");
            }
            else
            {
                lines.Add(scopeLine);
            }

            var nameExpression = parameterPath.EndsWith(".name", StringComparison.Ordinal)
                ? parameterPath
                : $"{parameterPath}.name";
            lines.Add($"  name: {nameExpression}");
            lines.Add("}");
            WriteStatement(writer, ref statementCount, string.Join('\n', lines));
        }

        foreach (var statement in OrderStatements(statements).Where(statement => statement.Order >= 0))
        {
            WriteMainStatement(writer, ref statementCount, statement);
        }

        return new GeneratedFile(
            $"{component.ComponentCode}/infra/main.bicep",
            writer.ToString().TrimEnd('\n') + "\n");
    }

    private static void WriteImports(
        BicepWriter writer,
        IReadOnlyList<(ResourcePlan Resource, CatalogResourceTypeDescriptor Descriptor)> localResources,
        (ExternalReference Reference, CatalogResourceTypeDescriptor Descriptor)[] externalReferences,
        int externalRootCount)
    {
        var configTypes = localResources
            .Select(entry => (Type: BicepCatalogMetadata.TryGetConfig(entry.Descriptor), entry.Descriptor))
            .Where(entry => entry.Type is not null)
            .Select(entry => (Name: BicepCatalogMetadata.ReadString(entry.Type!.Value, "typeName")!,
                Order: BicepCatalogMetadata.ReadInt32(entry.Type!.Value, "order")))
            .DistinctBy(entry => entry.Name, StringComparer.Ordinal)
            .ToArray();
        var types = new List<string>();
        if (externalReferences.Length == 0)
        {
            types.AddRange(configTypes.Select(entry => entry.Name).Order(StringComparer.Ordinal));
            types.Add("resourceGroupsConfig");
        }
        else if (externalRootCount > 1)
        {
            types.AddRange(configTypes.OrderBy(entry => entry.Order).Select(entry => entry.Name));
            types.Add("externalResourcesConfig");
            types.Add("resourceGroupsConfig");
        }
        else
        {
            types.Add("resourceGroupsConfig");
            types.AddRange(externalReferences.Select(entry => GetExternalConfigType(entry.Descriptor))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
            types.AddRange(configTypes.OrderBy(entry => entry.Order).Select(entry => entry.Name));
        }

        types.Add("targetConfig");

        writer.BeginBlock("import {");
        foreach (var type in types)
        {
            writer.Line(type);
        }

        writer.EndBlock(" from './types.bicep'");
    }

    private static void WriteParameters(
        BicepWriter writer,
        IReadOnlyList<(ResourcePlan Resource, CatalogResourceTypeDescriptor Descriptor)> localResources,
        (ExternalReference Reference, CatalogResourceTypeDescriptor Descriptor)[] externalReferences,
        string[] externalRoots)
    {
        writer.Line("param target targetConfig");
        writer.Line("param resourceGroups resourceGroupsConfig");
        if (externalRoots.Length > 1)
        {
            writer.Line("param externalResources externalResourcesConfig");
        }
        else if (externalRoots.Length == 1)
        {
            var root = externalRoots[0];
            var rootReferences = externalReferences.Where(entry => string.Equals(
                GetExternalRoot(entry.Descriptor), root, StringComparison.Ordinal)).ToArray();
            var type = rootReferences.Any(entry => GetExternalConfigType(entry.Descriptor) == "sqlDatabaseReferenceConfig")
                ? "sqlDatabaseReferenceConfig"
                : "resourceReferenceConfig";
            writer.Line($"param {root} {type}");
        }

        foreach (var entry in localResources
            .Select(item => (item.Resource, item.Descriptor, Config: BicepCatalogMetadata.TryGetConfig(item.Descriptor)))
            .Where(item => item.Config is not null)
            .DistinctBy(item => BicepCatalogMetadata.ReadString(item.Config!.Value, "typeName"), StringComparer.Ordinal)
            .OrderBy(item => BicepCatalogMetadata.ReadInt32(item.Config!.Value, "order")))
        {
            var parameterName = BicepCatalogMetadata.ReadString(BicepCatalogMetadata.GetBicep(entry.Descriptor), "parameterName")
                ?? throw new InvalidOperationException(
                    $"Resource type '{entry.Descriptor.Type}' has no Bicep parameter name.");
            var config = entry.Config!.Value;
            writer.Line($"param {parameterName} {BicepCatalogMetadata.ReadString(config, "typeName")}");
        }

        var deliveryParameters = localResources.SelectMany(entry =>
        {
            var bicep = BicepCatalogMetadata.GetBicep(entry.Descriptor);
            return bicep.TryGetProperty("deliveryParameters", out var parameters)
                ? parameters.EnumerateArray().Select(parameter => (Owner: entry.Descriptor.Type, Definition: parameter))
                : [];
        }).ToArray();
        var localTypes = localResources.Select(entry => entry.Descriptor.Type).ToHashSet(StringComparer.Ordinal);
        var externalTypes = externalReferences.Select(entry => entry.Descriptor.Type).ToHashSet(StringComparer.Ordinal);
        foreach (var deliveryParameter in deliveryParameters)
        {
            if (!MatchesRequirements(deliveryParameter.Definition, localTypes, externalTypes))
            {
                continue;
            }

            var name = deliveryParameter.Definition.GetProperty("name").GetString()!;
            var description = deliveryParameter.Definition.GetProperty("description").GetString()!;
            WriteDeliveryParameter(writer, name, description);
        }
    }

    internal static void WriteDeliveryParameter(BicepWriter writer, string name, string description)
    {
        writer.Line();
        writer.Line($"@description({BicepCatalogMetadata.SingleQuoted(description)})");
        writer.Line($"param {name} string = ''");
    }

    private static bool MatchesRequirements(
        JsonElement definition,
        HashSet<string> localTypes,
        HashSet<string> externalTypes)
    {
        return Matches(definition, "requiresResourceTypes", localTypes)
            && Matches(definition, "requiresExternalTypes", externalTypes)
            && MatchesAny(definition, "anyResourceTypes", localTypes)
            && MatchesAny(definition, "anyExternalTypes", externalTypes);
    }

    private static bool Matches(JsonElement definition, string name, HashSet<string> available)
    {
        if (!definition.TryGetProperty(name, out var requirements))
        {
            return true;
        }

        return requirements.EnumerateArray().All(required => available.Contains(required.GetString()!));
    }

    private static bool MatchesAny(JsonElement definition, string name, HashSet<string> available)
    {
        if (!definition.TryGetProperty(name, out var requirements))
        {
            return true;
        }

        return requirements.EnumerateArray().Any(required => available.Contains(required.GetString()!));
    }

    private static string ExpandTemplate(
        string template,
        CatalogVersion catalog,
        string moduleReference)
    {
        template = template.Replace("{{moduleReference}}", moduleReference, StringComparison.Ordinal);
        foreach (var role in catalog.Roles)
        {
            template = template.Replace($"{{{{role:{role.Name}}}}}", role.DefinitionId, StringComparison.Ordinal);
        }

        var roleAssignmentApiVersion = catalog.Pins.NativeResources.TryGetValue("roleAssignment", out var roleAssignment)
            ? roleAssignment.ApiVersion
            : throw new InvalidOperationException("The pinned catalog has no role assignment definition.");
        template = template.Replace("{{roleAssignmentApiVersion}}", roleAssignmentApiVersion, StringComparison.Ordinal);
        foreach (var resourceType in catalog.ResourceTypes)
        {
            if (BicepCatalogMetadata.TryGetExisting(resourceType) is not { } existing)
            {
                continue;
            }

            var apiVersion = BicepCatalogMetadata.ReadString(existing, "apiVersion")!;
            var armType = resourceType.Definition.GetProperty("armType").GetString()!;
            template = template.Replace($"{{{{resource:{resourceType.Type}}}}}", $"{armType}@{apiVersion}", StringComparison.Ordinal);
        }

        template = template.Replace("{{startupImage}}", catalog.Pins.StartupImage.Reference, StringComparison.Ordinal);
        template = template.Replace("{{moduleReference}}", moduleReference, StringComparison.Ordinal);
        BicepCatalogMetadata.EnsureNoUnresolvedPlaceholders(template);
        return template;
    }

    private static string GetExternalParameterPath(string path, bool hasExternalContainer)
    {
        return hasExternalContainer ? $"externalResources.{path}" : path;
    }

    private static string GetExternalRoot(CatalogResourceTypeDescriptor descriptor)
    {
        var existing = BicepCatalogMetadata.TryGetExisting(descriptor)
            ?? throw new InvalidOperationException(
                $"Resource type '{descriptor.Type}' has no Bicep existing-resource metadata.");
        return BicepCatalogMetadata.ReadString(existing, "parameterName")!.Split('.')[0];
    }

    private static string GetExternalConfigType(CatalogResourceTypeDescriptor descriptor)
    {
        var existing = BicepCatalogMetadata.TryGetExisting(descriptor)
            ?? throw new InvalidOperationException(
                $"Resource type '{descriptor.Type}' has no Bicep existing-resource metadata.");
        return BicepCatalogMetadata.ReadString(existing, "configType")
            ?? (BicepCatalogMetadata.ReadString(existing, "parentType") is null
                ? "resourceReferenceConfig"
                : "sqlDatabaseReferenceConfig");
    }

    private static int GetExistingOrder(CatalogResourceTypeDescriptor descriptor)
    {
        var existing = BicepCatalogMetadata.TryGetExisting(descriptor);
        return existing is { } value ? BicepCatalogMetadata.ReadInt32(value, "order") : int.MaxValue;
    }

    private static string ResolveExternalSymbol(JsonElement existing, string componentCode)
    {
        if (existing.TryGetProperty("symbolAliases", out var aliases)
            && aliases.TryGetProperty(componentCode, out var alias)
            && alias.ValueKind == JsonValueKind.String)
        {
            return alias.GetString()!;
        }

        return BicepCatalogMetadata.ReadString(existing, "symbol")!;
    }

    private static CatalogResourceTypeDescriptor GetDescriptor(CatalogVersion catalog, string type) =>
        catalog.GetResourceType(type)
        ?? throw new InvalidOperationException($"The pinned catalog has no descriptor for resource type '{type}'.");

    private static void WriteStatement(BicepWriter writer, ref int statementCount, string content)
    {
        if (statementCount > 0)
        {
            writer.BlankLine();
        }

        foreach (var line in content.Replace("\r", string.Empty, StringComparison.Ordinal).Trim('\n').Split('\n'))
        {
            writer.Line(line);
        }

        statementCount++;
    }

    private static IOrderedEnumerable<MainStatement> OrderStatements(IEnumerable<MainStatement> statements) =>
        statements.OrderBy(statement => statement.Order)
            .ThenBy(statement => statement.ResourceType, StringComparer.Ordinal)
            .ThenBy(statement => statement.Index);

    private static void WriteMainStatement(BicepWriter writer, ref int statementCount, MainStatement statement)
    {
        var content = statement.SectionComment is null
            ? statement.Template
            : $"// {statement.SectionComment}\n{statement.Template}";
        WriteStatement(writer, ref statementCount, content);
    }

    private static void WriteHeader(BicepWriter writer, string projectCode) =>
        writer.Line($"// Généré par InfraFlowSculptor — projet {projectCode}. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.");

    private sealed record MainStatement(int Order, string ResourceType, int Index, string? SectionComment, string Template);
}
