using System.Text.RegularExpressions;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Targets;

namespace InfraFlowSculptor.Engine.Naming;

public sealed partial class NamingEngine
{
    private static readonly HashSet<string> SupportedTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "project", "component", "name", "abbr", "env", "region"
    };

    private static readonly string SupportedTokenList = string.Join(", ", SupportedTokens.Order(StringComparer.Ordinal));

    public static NamingPreviewResult Preview(ModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var issues = ValidateTemplates(snapshot);
        if (issues.Count > 0)
        {
            return new NamingPreviewResult([], issues);
        }

        var items = new List<NamingPreviewItem>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reportedExternalCollisions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingGlobalReservations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var organizationGlobalNames = snapshot.OrganizationGlobalNames
            .Where(reservation => reservation.ProjectId != snapshot.Project.Id)
            .GroupBy(reservation => reservation.ResourceType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(reservation => reservation.Name).ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        foreach (var component in snapshot.Components.OrderBy(component => component.Code, StringComparer.Ordinal))
        {
            var resourceTargets = component.Resources.ToDictionary(
                resource => resource.Id,
                resource => TargetResolver.ResolveResourceTargets(snapshot, component, resource));

            foreach (var resourceGroup in component.ResourceGroups.OrderBy(group => group.Name, StringComparer.Ordinal))
            {
                var groupTargets = TargetResolver.ResolveResourceGroupTargets(snapshot, component, resourceGroup);
                foreach (var groupTarget in groupTargets.Where(target => target.IsPresent))
                {
                    if (!snapshot.NamingRules.TryGetValue(BuiltInNamingRules.ResourceGroupType, out var groupRule))
                    {
                        issues.Add(new NamingIssue(
                            "RESOURCE_GROUP_NAMING_DESCRIPTOR_MISSING",
                            "Le descripteur de nommage des groupes de ressources est absent.")
                        {
                            ComponentId = component.Id,
                            ResourceGroupId = resourceGroup.Id,
                            TargetId = groupTarget.Target.Id
                        });
                        continue;
                    }

                    var groupForcedName = resourceGroup.ForcedNames.GetValueOrDefault(groupTarget.Target.Id);
                    var groupName = GenerateName(
                        snapshot,
                        component,
                        groupTarget.Target,
                        groupTarget.Region,
                        BuiltInNamingRules.ResourceGroupType,
                        resourceGroup.Name,
                        groupForcedName,
                        groupRule,
                        resourceGroup.Id,
                        isResourceGroup: true,
                        issues);
                    if (groupName is null)
                    {
                        continue;
                    }

                    var groupScope = $"subscription:{groupTarget.Target.SubscriptionId:D}";
                    CheckCollision(
                        "ResourceGroup",
                        groupName.Value,
                        groupScope,
                        component.Id,
                        resourceId: null,
                        resourceGroupId: resourceGroup.Id,
                        groupTarget.Target.Id,
                        issues,
                        usedNames);

                    foreach (var resource in component.Resources
                                 .Where(resource => resource.ResourceGroupId == resourceGroup.Id)
                                 .OrderBy(resource => resource.Type, StringComparer.Ordinal)
                                 .ThenBy(resource => resource.LogicalName, StringComparer.Ordinal))
                    {
                        var resolvedTarget = resourceTargets[resource.Id]
                            .SingleOrDefault(target => target.Target.Id == groupTarget.Target.Id);
                        if (resolvedTarget is null || !resolvedTarget.IsPresent)
                        {
                            continue;
                        }

                        if (resource.IsExisting)
                        {
                            var existingId = resource.ExistingIds.GetValueOrDefault(groupTarget.Target.Id);
                            if (string.IsNullOrWhiteSpace(existingId))
                            {
                                continue;
                            }

                            if (snapshot.NamingRules.TryGetValue(resource.Type, out var existingRule)
                                && string.Equals(existingRule.Uniqueness, NamingRuleSnapshot.GlobalUniqueness, StringComparison.OrdinalIgnoreCase))
                            {
                                var existingName = GetExistingResourceName(existingId);
                                var reservationKey = $"{resource.Type}\u001f{existingName}\u001f{resource.Id:D}";
                                if (existingGlobalReservations.Add(reservationKey))
                                {
                                    CheckCollision(
                                        resource.Type,
                                        existingName,
                                        "global",
                                        component.Id,
                                        resource.Id,
                                        resourceGroup.Id,
                                        groupTarget.Target.Id,
                                        issues,
                                        usedNames);
                                }

                                if (HasExternalGlobalReservation(organizationGlobalNames, resource.Type, existingName))
                                {
                                    AddExternalGlobalCollision(
                                        resource.Type,
                                        existingName,
                                        component.Id,
                                        resource.Id,
                                        resourceGroup.Id,
                                        groupTarget.Target.Id,
                                        reportedExternalCollisions,
                                        issues);
                                }
                            }

                            items.Add(new NamingPreviewItem(
                                groupTarget.Target.Code,
                                existingId,
                                groupName.Value)
                            {
                                ComponentId = component.Id,
                                ComponentCode = component.Code,
                                ResourceId = resource.Id,
                                ResourceGroupId = resourceGroup.Id,
                                ResourceType = resource.Type,
                                LogicalName = resource.LogicalName,
                                TargetId = groupTarget.Target.Id,
                                Explanation = new NamingExplanation(
                                    "ExistingResource",
                                    string.Empty,
                                    existingId,
                                    existingId,
                                    []),
                                IsExisting = true,
                                NameLength = existingId.Length,
                                ResourceGroupExplanation = groupName.Explanation
                            });
                            continue;
                        }

                        if (!snapshot.NamingRules.TryGetValue(resource.Type, out var rule))
                        {
                            issues.Add(new NamingIssue(
                                "RESOURCE_TYPE_UNKNOWN",
                                $"Le type « {resource.Type} » n’a pas de descripteur de nommage dans le catalogue du projet.")
                            {
                                ComponentId = component.Id,
                                ResourceId = resource.Id,
                                ResourceGroupId = resourceGroup.Id,
                                TargetId = groupTarget.Target.Id
                            });
                            continue;
                        }

                        var forcedName = resource.ForcedNames.GetValueOrDefault(groupTarget.Target.Id);
                        var resourceName = GenerateName(
                            snapshot,
                            component,
                            groupTarget.Target,
                            resolvedTarget.Region,
                            resource.Type,
                            resource.LogicalName,
                            forcedName,
                            rule,
                            resource.Id,
                            isResourceGroup: false,
                            issues);
                        if (resourceName is null)
                        {
                            continue;
                        }

                        var uniquenessScope = GetUniquenessScope(rule, groupTarget.Target, groupName.Value, resourceGroup.Id);
                        CheckCollision(
                            resource.Type,
                            resourceName.Value,
                            uniquenessScope,
                            component.Id,
                            resource.Id,
                            resourceGroup.Id,
                            groupTarget.Target.Id,
                            issues,
                            usedNames);

                        if (string.Equals(rule.Uniqueness, NamingRuleSnapshot.GlobalUniqueness, StringComparison.OrdinalIgnoreCase)
                            && HasExternalGlobalReservation(organizationGlobalNames, resource.Type, resourceName.Value))
                        {
                            AddExternalGlobalCollision(
                                resource.Type,
                                resourceName.Value,
                                component.Id,
                                resource.Id,
                                resourceGroup.Id,
                                groupTarget.Target.Id,
                                reportedExternalCollisions,
                                issues);
                        }

                        items.Add(new NamingPreviewItem(
                            groupTarget.Target.Code,
                            resourceName.Value,
                            groupName.Value)
                        {
                            ComponentId = component.Id,
                            ComponentCode = component.Code,
                            ResourceId = resource.Id,
                            ResourceGroupId = resourceGroup.Id,
                            ResourceType = resource.Type,
                            LogicalName = resource.LogicalName,
                            TargetId = groupTarget.Target.Id,
                            Explanation = resourceName.Explanation,
                            ResourceGroupExplanation = groupName.Explanation,
                            NameLength = resourceName.Value.Length,
                            MaximumLength = rule.MaximumLength
                        });
                    }
                }
            }

            ValidateChildren(snapshot, component, issues);
        }

        return new NamingPreviewResult(items, issues);
    }

    private static bool HasExternalGlobalReservation(
        Dictionary<string, HashSet<string>> organizationGlobalNames,
        string resourceType,
        string name) =>
        organizationGlobalNames.TryGetValue(resourceType, out var names) && names.Contains(name);

    private static void AddExternalGlobalCollision(
        string resourceType,
        string name,
        Guid componentId,
        Guid resourceId,
        Guid resourceGroupId,
        Guid targetId,
        HashSet<string> reportedExternalCollisions,
        List<NamingIssue> issues)
    {
        var reservationKey = $"{resourceType}\u001f{name}";
        if (!reportedExternalCollisions.Add(reservationKey))
        {
            return;
        }

        issues.Add(new NamingIssue(
            "VAL-NOM-COLLISION",
            $"Le nom Azure « {name} » existe déjà dans un autre projet de l’organisation.")
        {
            ComponentId = componentId,
            ResourceId = resourceId,
            ResourceGroupId = resourceGroupId,
            TargetId = targetId,
            Value = name
        });
    }

    public static IReadOnlyList<NamingIssue> ValidateTemplate(string template)
    {
        var issues = new List<NamingIssue>();
        AddTemplateIssues(template, issues);
        return issues;
    }

    private static List<NamingIssue> ValidateTemplates(ModelSnapshot snapshot)
    {
        var issues = new List<NamingIssue>();
        AddTemplateIssues(snapshot.Naming.DefaultTemplate, issues);
        AddTemplateIssues(snapshot.Naming.ResourceGroupTemplate, issues);
        foreach (var template in snapshot.Naming.TypeTemplates.Values)
        {
            AddTemplateIssues(template, issues);
        }

        foreach (var component in snapshot.Components)
        {
            if (component.Naming is not { } naming)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(naming.DefaultTemplate))
            {
                AddTemplateIssues(naming.DefaultTemplate, issues);
            }

            foreach (var template in naming.TypeTemplates.Values)
            {
                AddTemplateIssues(template, issues);
            }
        }

        return issues;
    }

    private static void AddTemplateIssues(string? template, List<NamingIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            issues.Add(new NamingIssue("NAMING_TEMPLATE_EMPTY", "Un gabarit de nommage ne peut pas être vide."));
            return;
        }

        if (template.Length > 200)
        {
            issues.Add(new NamingIssue("NAMING_TEMPLATE_LENGTH", "Un gabarit de nommage ne peut pas dépasser 200 caractères."));
        }

        var tokens = TokenPattern().Matches(template)
            .Select(match => match.Groups["name"].Value)
            .ToArray();
        var unknownTokens = tokens
            .Where(token => !SupportedTokens.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unknownTokens.Length > 0)
        {
            issues.Add(new NamingIssue(
                "NAMING_TOKEN_UNKNOWN",
                $"Jeton(s) de nommage inconnu(s) : {string.Join(", ", unknownTokens)}. Jetons acceptés : {SupportedTokenList}."));
        }

        if (!tokens.Contains("name", StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new NamingIssue("NAMING_NAME_TOKEN_REQUIRED", "Un gabarit doit contenir le jeton {name}."));
        }

        var literalText = TokenPattern().Replace(template, string.Empty);
        if (literalText.Contains('{', StringComparison.Ordinal)
            || literalText.Contains('}', StringComparison.Ordinal)
            || !TemplateLiteralPattern().IsMatch(literalText))
        {
            issues.Add(new NamingIssue(
                "NAMING_TEMPLATE_FORMAT",
                "Hors des jetons, un gabarit ne peut contenir que des minuscules, des chiffres, des tirets, des soulignés et des points."));
        }
    }

    private static GeneratedName? GenerateName(
        ModelSnapshot snapshot,
        ComponentSnapshot component,
        ResolvedTarget target,
        string region,
        string resourceType,
        string logicalName,
        string? forcedName,
        NamingRuleSnapshot rule,
        Guid objectId,
        bool isResourceGroup,
        List<NamingIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(forcedName))
        {
            if (!NameSanitizer.IsValid(forcedName, rule))
            {
                var limit = forcedName.Length > rule.MaximumLength ? rule.MaximumLength : rule.MinimumLength;
                issues.Add(new NamingIssue(
                    isResourceGroup ? "RESOURCE_GROUP_FORCED_NAME_INVALID" : "RESOURCE_FORCED_NAME_INVALID",
                    $"Le nom forcé « {forcedName} » ne respecte pas les contraintes de nommage de {resourceType}.")
                {
                    ComponentId = component.Id,
                    ResourceId = isResourceGroup ? null : objectId,
                    ResourceGroupId = isResourceGroup ? objectId : null,
                    TargetId = target.Id,
                    Value = forcedName,
                    ValueLength = forcedName.Length,
                    Limit = limit
                });
            }

            return new GeneratedName(
                forcedName,
                new NamingExplanation("Forced", string.Empty, forcedName, forcedName, []));
        }

        var template = NamingTemplateResolver.Resolve(
            snapshot.Naming,
            component.Naming,
            resourceType,
            isResourceGroup);
        var regionCode = string.Empty;
        if (TokenPattern().Matches(template.Template).Any(match =>
                string.Equals(match.Groups["name"].Value, "region", StringComparison.OrdinalIgnoreCase)))
        {
            if (!snapshot.RegionCodes.TryGetValue(region, out regionCode!))
            {
                issues.Add(new NamingIssue(
                    "VAL-NOM-REGION",
                    $"La région « {region} » n’a pas de code court dans le catalogue du projet.")
                {
                    ComponentId = component.Id,
                    ResourceId = isResourceGroup ? null : objectId,
                    ResourceGroupId = isResourceGroup ? objectId : null,
                    TargetId = target.Id,
                    Value = region
                });
                return null;
            }
        }

        var abbreviation = ResolveAbbreviation(snapshot.Naming, component.Naming, resourceType, rule.Abbreviation);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["project"] = snapshot.Project.Code,
            ["component"] = component.Code,
            ["name"] = logicalName,
            ["abbr"] = abbreviation,
            ["env"] = target.Code,
            ["region"] = regionCode
        };
        var rawName = TokenPattern().Replace(template.Template, match => values[match.Groups["name"].Value]);
        var sanitized = NameSanitizer.Sanitize(rawName, rule);
        AddGeneratedNameIssues(sanitized.Value, rule, component.Id, objectId, target.Id, isResourceGroup, issues);
        return new GeneratedName(
            sanitized.Value,
            new NamingExplanation(template.Level, template.Template, rawName, sanitized.Value, sanitized.Transformations));
    }

    private static string ResolveAbbreviation(
        NamingConventionSnapshot project,
        ComponentNamingSnapshot? component,
        string resourceType,
        string catalogAbbreviation)
    {
        if (component is not null && component.TypeAbbreviations.TryGetValue(resourceType, out var componentAbbreviation))
        {
            return componentAbbreviation;
        }

        if (project.TypeAbbreviations.TryGetValue(resourceType, out var projectAbbreviation))
        {
            return projectAbbreviation;
        }

        return catalogAbbreviation;
    }

    private static void AddGeneratedNameIssues(
        string value,
        NamingRuleSnapshot rule,
        Guid componentId,
        Guid objectId,
        Guid targetId,
        bool isResourceGroup,
        List<NamingIssue> issues)
    {
        Guid? resourceId = isResourceGroup ? null : objectId;
        Guid? groupId = isResourceGroup ? objectId : null;
        if (value.Length == 0)
        {
            issues.Add(new NamingIssue("VAL-NOM-VIDE", "Le nom est vide après assainissement.")
            {
                ComponentId = componentId,
                ResourceId = resourceId,
                ResourceGroupId = groupId,
                TargetId = targetId,
                Value = value,
                ValueLength = 0,
                Limit = rule.MinimumLength
            });
            return;
        }

        if (value.Length < rule.MinimumLength || value.Length > rule.MaximumLength)
        {
            var limit = value.Length < rule.MinimumLength ? rule.MinimumLength : rule.MaximumLength;
            issues.Add(new NamingIssue(
                "VAL-NOM-LONGUEUR",
                $"Le nom Azure « {value} » fait {value.Length} caractères; la limite est {limit}. Définissez un gabarit pour ce type ou un nom forcé.")
            {
                ComponentId = componentId,
                ResourceId = resourceId,
                ResourceGroupId = groupId,
                TargetId = targetId,
                Value = value,
                ValueLength = value.Length,
                Limit = limit
            });
            return;
        }

        if (!NameSanitizer.IsValid(value, rule))
        {
            issues.Add(new NamingIssue("VAL-NOM-INVALIDE", $"Le nom Azure « {value} » ne respecte pas le descripteur de {rule.Abbreviation}.")
            {
                ComponentId = componentId,
                ResourceId = resourceId,
                ResourceGroupId = groupId,
                TargetId = targetId,
                Value = value,
                ValueLength = value.Length,
                Limit = rule.MaximumLength
            });
        }
    }

    private static string GetUniquenessScope(
        NamingRuleSnapshot rule,
        ResolvedTarget target,
        string resourceGroupName,
        Guid resourceGroupId) =>
        rule.Uniqueness.ToLowerInvariant() switch
        {
            "global" => "global",
            "resourcegroup" => $"subscription:{target.SubscriptionId:D}:resource-group:{resourceGroupName}",
            "parent" => $"resource-group:{resourceGroupId:D}",
            _ => $"target:{target.Id:D}"
        };

    private static string GetExistingResourceName(string resourceId)
    {
        var normalizedId = resourceId.TrimEnd('/');
        var separatorIndex = normalizedId.LastIndexOf('/');
        return separatorIndex < 0 ? normalizedId : normalizedId[(separatorIndex + 1)..];
    }

    private static void CheckCollision(
        string resourceType,
        string name,
        string scope,
        Guid componentId,
        Guid? resourceId,
        Guid? resourceGroupId,
        Guid targetId,
        List<NamingIssue> issues,
        HashSet<string> usedNames)
    {
        var key = $"{resourceType}\u001f{scope}\u001f{name}";
        if (usedNames.Add(key))
        {
            return;
        }

        issues.Add(new NamingIssue(
            "VAL-NOM-COLLISION",
            $"Le nom Azure « {name} » est déjà utilisé dans la même portée d’unicité.")
        {
            ComponentId = componentId,
            ResourceId = resourceId,
            ResourceGroupId = resourceGroupId,
            TargetId = targetId,
            Value = name
        });
    }

    private static void ValidateChildren(
        ModelSnapshot snapshot,
        ComponentSnapshot component,
        List<NamingIssue> issues)
    {
        foreach (var resource in component.Resources)
        {
            var childNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in resource.Children)
            {
                var ruleKey = $"{resource.Type}/{child.Type}";
                if (!snapshot.NamingRules.TryGetValue(ruleKey, out var rule))
                {
                    issues.Add(new NamingIssue(
                        "RESOURCE_CHILD_TYPE_UNKNOWN",
                        $"Le type d’enfant « {child.Type} » n’est pas déclaré par le descripteur {resource.Type}.")
                    {
                        ComponentId = component.Id,
                        ResourceId = resource.Id,
                        Value = child.Name
                    });
                    continue;
                }

                if (!NameSanitizer.IsValid(child.Name, rule))
                {
                    issues.Add(new NamingIssue(
                        "RESOURCE_CHILD_NAME_INVALID",
                        $"Le nom d’enfant « {child.Name} » ne respecte pas les contraintes de son descripteur.")
                    {
                        ComponentId = component.Id,
                        ResourceId = resource.Id,
                        Value = child.Name,
                        ValueLength = child.Name.Length,
                        Limit = rule.MaximumLength
                    });
                }

                if (!childNames.Add(child.Name))
                {
                    issues.Add(new NamingIssue(
                        "VAL-NOM-COLLISION",
                        $"Le nom d’enfant « {child.Name} » est déjà utilisé dans sa ressource parente.")
                    {
                        ComponentId = component.Id,
                        ResourceId = resource.Id,
                        Value = child.Name
                    });
                }

            }
        }
    }

    [GeneratedRegex("\\{(?<name>[^{}]+)\\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    [GeneratedRegex("^[a-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateLiteralPattern();

    private sealed record GeneratedName(string Value, NamingExplanation Explanation);
}
