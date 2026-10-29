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
        foreach (var step in configuredSteps.Where(step => step.Enabled))
        {
            var descriptor = catalogSteps.SingleOrDefault(candidate =>
                string.Equals(candidate.Name, step.Name, StringComparison.OrdinalIgnoreCase));
            if (descriptor is null)
            {
                findings.Add(new ApplicationStepFinding(
                    "VAL-APP-ETAPE",
                    step.Name,
                    null,
                    $"L’étape « {step.Name} » n’est pas disponible dans le catalogue épinglé."));
                continue;
            }

            foreach (var parameter in descriptor.Parameters)
            {
                var configuredValue = step.Parameters.SingleOrDefault(value =>
                    string.Equals(value.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))?.Value;
                if (parameter.DefaultValue is null && string.IsNullOrWhiteSpace(configuredValue))
                {
                    findings.Add(new ApplicationStepFinding(
                        "VAL-APP-ETAPE",
                        step.Name,
                        parameter.Name,
                        $"L’étape « {descriptor.LabelFr} » exige une valeur pour « {parameter.Name} »."));
                    continue;
                }

                var value = configuredValue ?? parameter.DefaultValue;
                if (parameter.Values.Length > 0 && value is not null
                    && !parameter.Values.Contains(value, StringComparer.OrdinalIgnoreCase))
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
