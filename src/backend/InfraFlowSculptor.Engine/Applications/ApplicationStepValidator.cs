using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Engine.Applications;

public static class ApplicationStepValidator
{
    public static IReadOnlyList<ApplicationStepFinding> Validate(
        IReadOnlyCollection<ApplicationStepDefinition> configuredSteps,
        IReadOnlyCollection<CatalogPipelineStepDescriptor> catalogSteps)
    {
        ArgumentNullException.ThrowIfNull(configuredSteps);
        ArgumentNullException.ThrowIfNull(catalogSteps);

        var findings = new List<ApplicationStepFinding>();
        var steps = configuredSteps.ToArray();
        var duplicateStepNames = steps
            .Where(step => step.Enabled)
            .GroupBy(step => step.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var name in duplicateStepNames.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            findings.Add(new ApplicationStepFinding(
                "VAL-APP-ETAPE",
                name,
                null,
                $"L’étape « {name} » est configurée plusieurs fois."));
        }

        foreach (var step in steps.Where(step => !step.Enabled || !duplicateStepNames.Contains(step.Name)))
        {
            var matchingDescriptors = catalogSteps
                .Where(candidate => string.Equals(candidate.Name, step.Name, StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            if (matchingDescriptors.Length == 0)
            {
                findings.Add(new ApplicationStepFinding(
                    "VAL-APP-ETAPE",
                    step.Name,
                    null,
                    $"L’étape « {step.Name} » n’est pas disponible dans le catalogue épinglé."));
                continue;
            }

            if (matchingDescriptors.Length > 1)
            {
                findings.Add(new ApplicationStepFinding(
                    "VAL-APP-ETAPE",
                    step.Name,
                    null,
                    $"L’étape « {step.Name} » apparaît plusieurs fois dans le catalogue épinglé."));
                continue;
            }

            var descriptor = matchingDescriptors[0];
            var declaredParameterNames = descriptor.Parameters
                .Select(parameter => parameter.Name)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var name in step.Parameters
                         .Select(parameter => parameter.Name)
                         .Where(name => !declaredParameterNames.Contains(name))
                         .Distinct(StringComparer.Ordinal))
            {
                findings.Add(new ApplicationStepFinding(
                    "VAL-APP-ETAPE",
                    step.Name,
                    name,
                    $"Le paramètre « {name} » n’est pas déclaré pour l’étape « {descriptor.LabelFr} » dans le catalogue épinglé."));
            }

            var duplicateDescriptorParameter = descriptor.Parameters
                .GroupBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateDescriptorParameter is not null)
            {
                findings.Add(new ApplicationStepFinding(
                    "VAL-APP-ETAPE",
                    step.Name,
                    duplicateDescriptorParameter.Key,
                    $"Le paramètre « {duplicateDescriptorParameter.Key} » est déclaré plusieurs fois pour l’étape « {descriptor.LabelFr} » dans le catalogue épinglé."));
                continue;
            }

            foreach (var parameter in descriptor.Parameters)
            {
                var matchingValues = step.Parameters
                    .Where(value => string.Equals(value.Name, parameter.Name, StringComparison.Ordinal))
                    .Take(2)
                    .ToArray();
                if (matchingValues.Length > 1)
                {
                    findings.Add(new ApplicationStepFinding(
                        "VAL-APP-ETAPE",
                        step.Name,
                        parameter.Name,
                        $"Le paramètre « {parameter.Name} » est configuré plusieurs fois pour l’étape « {descriptor.LabelFr} »."));
                    continue;
                }

                var configuredValue = matchingValues.FirstOrDefault()?.Value;
                if (step.Enabled && parameter.DefaultValue is null && string.IsNullOrWhiteSpace(configuredValue))
                {
                    findings.Add(new ApplicationStepFinding(
                        "VAL-APP-ETAPE",
                        step.Name,
                        parameter.Name,
                        $"L’étape « {descriptor.LabelFr} » exige une valeur pour « {parameter.Name} »."));
                    continue;
                }

                var value = string.IsNullOrWhiteSpace(configuredValue) ? parameter.DefaultValue : configuredValue;
                if (parameter.Values.Length > 0 && value is not null
                    && !parameter.Values.Contains(value, StringComparer.Ordinal))
                {
                    findings.Add(new ApplicationStepFinding(
                        "VAL-APP-ETAPE",
                        step.Name,
                        parameter.Name,
                        $"La valeur « {value} » n’est pas autorisée pour « {parameter.Name} » dans l’étape « {descriptor.LabelFr} »."));
                }
            }
        }

        return findings;
    }
}
