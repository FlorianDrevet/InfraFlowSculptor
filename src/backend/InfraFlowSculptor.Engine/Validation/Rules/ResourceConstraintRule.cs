using System.Globalization;
using System.Security.Cryptography;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Targets;
using InfraFlowSculptor.Engine.Validation.Constraints;
using InfraFlowSculptor.Engine.Values;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ResourceConstraintRule : IValidationRule
{
    private const string ExposureProperty = "exposure";
    private readonly ResourceConstraintEvaluatorRegistry constraintEvaluators;

    public ResourceConstraintRule()
        : this(ResourceConstraintEvaluatorRegistry.CreateDefault())
    {
    }

    public ResourceConstraintRule(ResourceConstraintEvaluatorRegistry constraintEvaluators)
    {
        ArgumentNullException.ThrowIfNull(constraintEvaluators);
        this.constraintEvaluators = constraintEvaluators;
    }

    public string Code => "VAL-RES-CONTRAINTE";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var findings = new List<ValidationFinding>();
        foreach (var (component, resource) in context.Resources)
        {
            if (!context.ResourceTypes.TryGetValue(resource.Type, out var descriptor))
            {
                continue;
            }

            var definitions = descriptor.Definition.GetProperty("properties").EnumerateArray()
                .ToDictionary(property => property.GetProperty("name").GetString()!, StringComparer.Ordinal);
            var declaredProperties = definitions.Keys.ToHashSet(StringComparer.Ordinal);
            foreach (var target in TargetResolver.ResolveResourceTargets(context.Snapshot, component, resource)
                         .Where(value => value.IsPresent))
            {
                var resolution = EffectiveValueResolver.ResolveDetailed(resource, target.Target.Id, descriptor.Definition);
                foreach (var conditionIssue in resolution.UnresolvedConditions.Where(ShouldReportUnresolvedCondition))
                {
                    findings.Add(context.Finding(
                        Code,
                        Severity,
                        new ObjectRef(ObjectType.Resource, resource.Id),
                        $"La contrainte du catalogue pour « {conditionIssue.PropertyName} » ne peut pas être évaluée : elle dépend de « {conditionIssue.DependencyName} », qui n’est pas déclaré.",
                        $"Catalog constraint for '{conditionIssue.PropertyName}' cannot be evaluated because it depends on undeclared property '{conditionIssue.DependencyName}'.",
                        target.Target.Id,
                        detailKey: CreateBoundedDetailKey(
                            "condition",
                            $"{conditionIssue.PropertyName}\0{conditionIssue.DependencyName}")));
                }

                foreach (var value in resolution.Values
                             .Where(value => value.Value is { ValueKind: not (System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined) }))
                {
                    if (!definitions.TryGetValue(value.Name, out var definition))
                    {
                        continue;
                    }

                    var failure = CatalogPropertyValueValidator.Validate(value.Value!.Value, definition);
                    if (failure is null)
                    {
                        continue;
                    }

                    var (reasonFr, reasonEn) = DescribePropertyValueFailure(failure);

                    findings.Add(context.Finding(
                        Code,
                        Severity,
                        new ObjectRef(ObjectType.Resource, resource.Id),
                        $"La valeur effective de « {value.Name} » pour « {resource.LogicalName} » ne respecte pas le catalogue : {reasonFr}.",
                        $"The effective value of '{value.Name}' for '{resource.LogicalName}' violates the catalog: {reasonEn}.",
                        target.Target.Id,
                        detailKey: CreateBoundedDetailKey("value", value.Name)));
                }

                var effectiveValues = resolution.Values
                    .Where(value => value.Value is { ValueKind: not (System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined) })
                    .ToDictionary(value => value.Name, value => value.Value!.Value, StringComparer.Ordinal);
                if (descriptor.Definition.TryGetProperty("constraints", out var constraints))
                {
                    EvaluateConstraints(
                        constraints,
                        effectiveValues,
                        declaredProperties,
                        context,
                        resource,
                        target.Target.Id,
                        itemContext: null,
                        findings);
                }

                foreach (var (propertyName, definition) in definitions)
                {
                    if (definition.TryGetProperty("allowedFrom", out var allowedFrom)
                        && effectiveValues.TryGetValue(propertyName, out var selectedValue))
                    {
                        EvaluateAllowedValueSource(
                            allowedFrom,
                            propertyName,
                            selectedValue,
                            context,
                            resource,
                            target.Target.Id,
                            findings);
                    }

                    if (!definition.TryGetProperty("items", out var items)
                        || !items.TryGetProperty("constraints", out var itemConstraints)
                        || !effectiveValues.TryGetValue(propertyName, out var propertyValue)
                        || propertyValue.ValueKind != System.Text.Json.JsonValueKind.Array)
                    {
                        continue;
                    }

                    var declaredItemMembers = items.GetProperty("members").EnumerateArray()
                        .Select(member => member.GetProperty("name").GetString()!)
                        .ToHashSet(StringComparer.Ordinal);
                    var duplicateUniqueKeyIndexes = FindDuplicateUniqueKeyIndexes(propertyValue, items);

                    var itemIndex = 0;
                    foreach (var item in propertyValue.EnumerateArray())
                    {
                        if (item.ValueKind == System.Text.Json.JsonValueKind.Object
                            && !duplicateUniqueKeyIndexes.Contains(itemIndex))
                        {
                            var itemValues = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
                            var hasUniqueMembers = true;
                            foreach (var member in item.EnumerateObject())
                            {
                                if (!itemValues.TryAdd(member.Name, member.Value))
                                {
                                    hasUniqueMembers = false;
                                    break;
                                }
                            }

                            if (hasUniqueMembers)
                            {
                                var itemContext = CreateItemConstraintContext(item, items, itemIndex, propertyName);
                                EvaluateConstraints(
                                    itemConstraints,
                                    itemValues,
                                    declaredItemMembers,
                                    context,
                                    resource,
                                    target.Target.Id,
                                    itemContext,
                                    findings);
                            }
                        }

                        itemIndex++;
                    }
                }
            }
        }

        return findings;
    }

    private void EvaluateConstraints(
        System.Text.Json.JsonElement constraints,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> values,
        HashSet<string> declaredValueNames,
        ValidationContext context,
        ResourceSnapshot resource,
        Guid targetId,
        ItemConstraintContext? itemContext,
        List<ValidationFinding> findings)
    {
        foreach (var constraint in constraints.EnumerateArray())
        {
            var kind = constraint.GetProperty("kind").GetString()!;
            var id = constraint.GetProperty("id").GetString()!;
            ResourceConstraintFailure? failure;
            if (!constraintEvaluators.TryGet(kind, out var evaluator))
            {
                failure = new ResourceConstraintFailure(
                    $"le type de contrainte « {kind} » n’est pas reconnu",
                    $"constraint kind '{kind}' is not recognized");
            }
            else
            {
                var unknownProperty = evaluator.GetReferencedProperties(constraint)
                    .FirstOrDefault(property => !declaredValueNames.Contains(property));
                failure = unknownProperty is null
                    ? evaluator.GetFailure(constraint, values)
                    : new ResourceConstraintFailure(
                        $"la propriété « {unknownProperty} » n’est pas déclarée",
                        $"property '{unknownProperty}' is not declared");
            }
            if (failure is null)
            {
                continue;
            }

            var detailKey = itemContext is null
                ? $"constraint-{id}"
                : $"constraint-{id}:{itemContext.Value.DetailSuffix}";
            findings.Add(context.Finding(
                Code,
                Severity,
                new ObjectRef(ObjectType.Resource, resource.Id),
                $"La contrainte « {id} » du catalogue n’est pas respectée pour « {resource.LogicalName} »{itemContext?.MessageSuffixFr} : {failure.French}.",
                $"Catalog constraint '{id}' is not satisfied for '{resource.LogicalName}'{itemContext?.MessageSuffixEn}: {failure.English}.",
                targetId,
                detailKey: detailKey));
        }
    }

    private static ItemConstraintContext CreateItemConstraintContext(
        System.Text.Json.JsonElement item,
        System.Text.Json.JsonElement itemDefinition,
        int itemIndex,
        string arrayPropertyName)
    {
        if (itemDefinition.TryGetProperty("uniqueBy", out var uniqueByElement))
        {
            var uniqueBy = uniqueByElement.GetString()!;
            if (item.TryGetProperty(uniqueBy, out var uniqueValue)
                && uniqueValue.ValueKind is System.Text.Json.JsonValueKind.String
                    or System.Text.Json.JsonValueKind.Number
                    or System.Text.Json.JsonValueKind.True
                    or System.Text.Json.JsonValueKind.False)
            {
                var canonicalValue = uniqueValue.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String => uniqueValue.GetString()!,
                    System.Text.Json.JsonValueKind.Number when uniqueValue.TryGetDecimal(out var number) =>
                        number.ToString("G29", CultureInfo.InvariantCulture),
                    _ => uniqueValue.GetRawText()
                };
                var identityKey = CatalogPropertyValueValidator.GetUniqueValueIdentityKey(uniqueValue);
                var hashInput = System.Text.Encoding.UTF8.GetBytes($"{arrayPropertyName}\0{uniqueBy}\0{identityKey}");
                var hash = Convert.ToHexString(SHA256.HashData(hashInput)).ToLowerInvariant();
                var displayValue = ToSafeDisplayValue(canonicalValue);
                var label = $"{CatalogValueDisplay.SafeDisplay(uniqueBy)} = {displayValue}";
                return new ItemConstraintContext($"item-{hash}", $" (élément « {label} »)", $" (item '{label}')");
            }
        }

        var itemNumber = (itemIndex + 1).ToString(CultureInfo.InvariantCulture);
        var suffixHash = HashIdentity($"{arrayPropertyName}\0index\0{itemIndex.ToString(CultureInfo.InvariantCulture)}");
        return new ItemConstraintContext($"item-{suffixHash}", $" (élément n° {itemNumber})", $" (item #{itemNumber})");
    }

    private static HashSet<int> FindDuplicateUniqueKeyIndexes(
        System.Text.Json.JsonElement items,
        System.Text.Json.JsonElement itemDefinition)
    {
        var duplicateIndexes = new HashSet<int>();
        if (!itemDefinition.TryGetProperty("uniqueBy", out var uniqueByElement))
        {
            return duplicateIndexes;
        }

        var uniqueBy = uniqueByElement.GetString()!;
        var seenValues = new Dictionary<string, int>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind == System.Text.Json.JsonValueKind.Object
                && item.TryGetProperty(uniqueBy, out var uniqueValue))
            {
                var identityKey = CatalogPropertyValueValidator.GetUniqueValueIdentityKey(uniqueValue);
                if (seenValues.TryGetValue(identityKey, out var previousIndex))
                {
                    duplicateIndexes.Add(previousIndex);
                    duplicateIndexes.Add(index);
                }
                else
                {
                    seenValues.Add(identityKey, index);
                }
            }

            index++;
        }

        return duplicateIndexes;
    }

    private static string ToSafeDisplayValue(string value)
    {
        return CatalogValueDisplay.SafeDisplay(value);
    }

    private static string CreateBoundedDetailKey(string prefix, string value)
    {
        var detailKey = $"{prefix}-{value}";
        return detailKey.Length <= 128
            && string.Equals(CatalogValueDisplay.SafeDisplay(value), value, StringComparison.Ordinal)
            ? detailKey
            : $"{prefix}-{HashIdentity(value)}";
    }

    private static string HashIdentity(string value) => Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))
        .ToLowerInvariant();

    private readonly record struct ItemConstraintContext(
        string DetailSuffix,
        string MessageSuffixFr,
        string MessageSuffixEn);

    private static void EvaluateAllowedValueSource(
        System.Text.Json.JsonElement allowedFrom,
        string propertyName,
        System.Text.Json.JsonElement selectedValue,
        ValidationContext context,
        ResourceSnapshot resource,
        Guid targetId,
        List<ValidationFinding> findings)
    {
        if (selectedValue.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return;
        }

        var linkKindText = allowedFrom.GetProperty("linkKind").GetString()!;
        if (!Enum.TryParse<LinkKind>(linkKindText, ignoreCase: false, out var linkKind)
            || !context.LinkKinds.Supports(linkKind))
        {
            AddAllowedValueSourceFinding(
                findings,
                context,
                resource,
                targetId,
                propertyName,
                $"le type de liaison « {linkKindText} » n’est pas reconnu",
                $"link kind '{linkKindText}' is not recognized");
            return;
        }

        var links = context.LinkKinds.GetFromSource(context.Snapshot, resource.Id, linkKind);
        if (links.Count == 0)
        {
            return;
        }

        if (links.Count != 1)
        {
            AddAllowedValueSourceFinding(
                findings,
                context,
                resource,
                targetId,
                propertyName,
                $"la source de valeurs a trouvé {links.Count} liaisons « {linkKindText} »",
                $"the value source found {links.Count} '{linkKindText}' links");
            return;
        }

        var link = links[0];
        if (!context.TryGetResource(link.TargetResourceId, out var targetComponent, out var targetResource)
            || !context.ResourceTypes.TryGetValue(targetResource.Type, out var targetDescriptor))
        {
            return;
        }

        var targetIsPresent = TargetResolver.ResolveResourceTargets(context.Snapshot, targetComponent, targetResource)
            .Any(target => target.Target.Id == targetId && target.IsPresent);
        if (!targetIsPresent)
        {
            return;
        }

        if (targetResource.IsExisting)
        {
            return;
        }

        var targetValues = EffectiveValueResolver.Resolve(targetResource, targetId, targetDescriptor.Definition)
            .Where(value => value.Value is { ValueKind: not (System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined) })
            .ToDictionary(value => value.Name, value => value.Value!.Value, StringComparer.Ordinal);
        var targetProperty = allowedFrom.GetProperty("targetProperty").GetString()!;
        var targetMember = allowedFrom.GetProperty("targetMember").GetString()!;
        var targetPropertyDefinition = targetDescriptor.Definition.GetProperty("properties").EnumerateArray()
            .FirstOrDefault(property => property.GetProperty("name").GetString() == targetProperty);
        if (targetPropertyDefinition.ValueKind != System.Text.Json.JsonValueKind.Object
            || !targetPropertyDefinition.TryGetProperty("items", out var itemDefinition)
            || !itemDefinition.TryGetProperty("members", out var members)
            || !members.EnumerateArray().Any(member => member.GetProperty("name").GetString() == targetMember))
        {
            AddAllowedValueSourceFinding(
                findings,
                context,
                resource,
                targetId,
                propertyName,
                $"la propriété liée « {targetProperty}.{targetMember} » n’est pas déclarée",
                $"linked property '{targetProperty}.{targetMember}' is not declared");
            return;
        }

        var allowedValues = new HashSet<string>(StringComparer.Ordinal);
        foreach (var implicitValue in allowedFrom.GetProperty("implicitValues").EnumerateArray())
        {
            if (implicitValue.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                allowedValues.Add(implicitValue.GetString()!);
            }
        }

        if (targetValues.TryGetValue(targetProperty, out var targetValue))
        {
            if (targetValue.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                AddAllowedValueSourceFinding(
                    findings,
                    context,
                    resource,
                    targetId,
                    propertyName,
                    $"la propriété liée « {targetProperty} » n’est pas un tableau",
                    $"linked property '{targetProperty}' is not an array");
                return;
            }

            foreach (var item in targetValue.EnumerateArray())
            {
                if (item.ValueKind == System.Text.Json.JsonValueKind.Object
                    && item.TryGetProperty(targetMember, out var memberValue)
                    && memberValue.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    allowedValues.Add(memberValue.GetString()!);
                }
            }
        }

        var selected = selectedValue.GetString()!;
        if (allowedValues.Contains(selected))
        {
            return;
        }

        var displaySelected = CatalogValueDisplay.SafeDisplay(selected);
        AddAllowedValueSourceFinding(
            findings,
            context,
            resource,
            targetId,
            propertyName,
            $"le profil « {displaySelected} » n’est pas déclaré par la ressource liée",
            $"profile '{displaySelected}' is not declared by the linked resource");
    }

    private static void AddAllowedValueSourceFinding(
        List<ValidationFinding> findings,
        ValidationContext context,
        Engine.Model.ResourceSnapshot resource,
        Guid targetId,
        string propertyName,
        string messageFr,
        string messageEn) => findings.Add(context.Finding(
        "VAL-RES-CONTRAINTE",
        FindingSeverity.Error,
        new ObjectRef(ObjectType.Resource, resource.Id),
        $"La valeur de « {propertyName} » pour « {resource.LogicalName} » ne respecte pas le catalogue : {messageFr}.",
        $"The value of '{propertyName}' for '{resource.LogicalName}' violates the catalog: {messageEn}.",
        targetId,
        detailKey: CreateBoundedDetailKey("allowed-from", propertyName)));

    private static bool ShouldReportUnresolvedCondition(EffectiveValueConditionIssue issue) =>
        issue.HasNonDefaultValue || string.Equals(issue.PropertyName, ExposureProperty, StringComparison.Ordinal);

    private static (string French, string English) DescribePropertyValueFailure(CatalogPropertyValueFailure failure) =>
        failure.Kind switch
        {
            CatalogPropertyValueFailureKind.Type => ("la valeur ne correspond pas au type du catalogue", "the value does not match the catalog type"),
            CatalogPropertyValueFailureKind.Range => ("la valeur est hors de la plage définie par le catalogue", "the value is outside the catalog range"),
            CatalogPropertyValueFailureKind.Length => ("la longueur ne respecte pas les limites du catalogue", "the length is outside the catalog limits"),
            CatalogPropertyValueFailureKind.Pattern => ("le format ne correspond pas au motif du catalogue", "the value does not match the catalog pattern"),
            CatalogPropertyValueFailureKind.Format => ("la valeur ne respecte pas le format du catalogue", "the value does not match the catalog format"),
            CatalogPropertyValueFailureKind.Deprecated => ("la valeur est déconseillée par le catalogue", "the value is deprecated by the catalog"),
            CatalogPropertyValueFailureKind.Forbidden => ("la valeur est interdite par le catalogue", "the value is forbidden by the catalog"),
            CatalogPropertyValueFailureKind.EnumValue => ("la valeur n’est pas dans la liste autorisée", "the value is not in the allowed list"),
            CatalogPropertyValueFailureKind.ArrayLength => ("le tableau dépasse la taille maximale du catalogue", "the array exceeds the catalog maximum length"),
            CatalogPropertyValueFailureKind.ItemType => ("un élément ne correspond pas au type du catalogue", "an item does not match the catalog type"),
            CatalogPropertyValueFailureKind.DuplicateItemValue => ($"plusieurs éléments partagent la même valeur « {failure.DisplayMemberName} »", $"multiple items share the same '{failure.DisplayMemberName}' value"),
            CatalogPropertyValueFailureKind.UnknownObjectMember => ($"le membre « {failure.DisplayMemberName} » n’est pas déclaré", $"member '{failure.DisplayMemberName}' is not declared"),
            CatalogPropertyValueFailureKind.DuplicateObjectMember => ($"le membre « {failure.DisplayMemberName} » apparaît plusieurs fois", $"member '{failure.DisplayMemberName}' appears more than once"),
            CatalogPropertyValueFailureKind.RequiredObjectMember => ($"le membre requis « {failure.DisplayMemberName} » est absent ou nul", $"required member '{failure.DisplayMemberName}' is missing or null"),
            _ => throw new InvalidOperationException($"Unknown catalog property validation failure: {failure.Kind}.")
        };
}
