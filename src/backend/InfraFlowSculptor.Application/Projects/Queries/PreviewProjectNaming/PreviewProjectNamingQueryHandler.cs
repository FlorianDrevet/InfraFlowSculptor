using ErrorOr;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Application.Naming.Availability;
using InfraFlowSculptor.Application.Naming;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Queries.PreviewProjectNaming;

public sealed class PreviewProjectNamingQueryHandler(
    ModelSnapshotBuilder snapshotBuilder,
    NameAvailabilityService nameAvailability,
    ICatalogProvider catalogProvider)
    : IQueryHandler<PreviewProjectNamingQuery, ErrorOr<NamingPreviewResult>>
{
    public async ValueTask<ErrorOr<NamingPreviewResult>> Handle(
        PreviewProjectNamingQuery query,
        CancellationToken cancellationToken)
    {
        var snapshot = await snapshotBuilder.BuildWithOrganizationNamingReservationsAsync(query.ProjectId, cancellationToken);
        if (snapshot.IsError)
        {
            return snapshot.Errors;
        }

        var catalog = catalogProvider.GetVersion(snapshot.Value.Project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var typeTemplates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var abbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in query.TypeOverrides)
        {
            var validated = ResourceTypeNamingOverride.Create(value.Type, value.Template, value.Abbreviation);
            if (validated.IsError)
            {
                return validated.Errors;
            }

            var validType = NamingOverrideTypeValidator.Validate(
                [new NamingOverrideValue(validated.Value.Type, validated.Value.Template, validated.Value.Abbreviation)],
                catalog);
            if (validType.IsError)
            {
                return validType.Errors;
            }

            if (!typeTemplates.TryAdd(validated.Value.Type, validated.Value.Template ?? string.Empty)
                || !abbreviations.TryAdd(validated.Value.Type, validated.Value.Abbreviation ?? string.Empty))
            {
                return Error.Validation("NAMING_TYPE_DUPLICATE", "Un seul réglage de nommage est autorisé par type.");
            }
        }

        typeTemplates = typeTemplates
            .Where(value => !string.IsNullOrEmpty(value.Value))
            .ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase);
        abbreviations = abbreviations
            .Where(value => !string.IsNullOrEmpty(value.Value))
            .ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase);

        var updated = snapshot.Value with
        {
            Naming = snapshot.Value.Naming with
            {
                Preset = query.Preset,
                DefaultTemplate = query.DefaultTemplate,
                ResourceGroupTemplate = query.ResourceGroupTemplate,
                TypeTemplates = typeTemplates,
                TypeAbbreviations = abbreviations
            }
        };
        var preview = NamingEngine.Preview(updated);
        var issues = preview.Issues.ToList();
        var availabilityCandidates = preview.Items
            .Select(item =>
            {
                snapshot.Value.NamingRules.TryGetValue(item.ResourceType, out var rule);
                return (Item: item, Rule: rule);
            })
            .Where(candidate => !candidate.Item.IsExisting
                && candidate.Rule is not null
                && string.Equals(candidate.Rule.Uniqueness, NamingRuleSnapshot.GlobalUniqueness, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(candidate.Rule.AvailabilityDomain))
            .ToArray();

        foreach (var domainGroup in availabilityCandidates.GroupBy(
                     candidate => candidate.Rule!.AvailabilityDomain!,
                     StringComparer.OrdinalIgnoreCase))
        {
            var candidates = domainGroup.ToArray();
            var statuses = await nameAvailability.GetStatusesAsync(
                domainGroup.Key,
                candidates.Select(candidate => candidate.Item.ResourceName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                cancellationToken);
            foreach (var candidate in candidates)
            {
                var item = candidate.Item;
                var rule = candidate.Rule!;
                if (statuses.GetValueOrDefault(item.ResourceName) != NameAvailabilityState.Resolved)
                {
                    continue;
                }

                var fullName = $"{item.ResourceName}.{rule.AvailabilityDomain}";
                issues.Add(new NamingIssue(
                    NameAvailabilityIssueCodes.Resolved,
                    $"Le nom « {fullName} » est résolu dans le DNS public. C'est normal si la ressource est déjà déployée.")
                {
                    Severity = NamingIssueSeverity.Info,
                    ComponentId = item.ComponentId,
                    ResourceId = item.ResourceId,
                    TargetId = item.TargetId,
                    Value = fullName
                });
            }
        }

        return preview with { Issues = issues };
    }
}
