using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.AzureDevOps;

public sealed class AzureDevOpsStepTranslator : IStepTranslator
{
    private const string AzurePipelinesProvider = "AzurePipelines";
    private const string TemplatePrefix = ".ifs/templates/steps/";

    public CiPlatform Key => CiPlatform.AzureDevOps;

    public PipelineStepTranslation Translate(CatalogPipelineStepDescriptor descriptor, ApplicationStepPlan applicationStep)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(applicationStep);
        if (!string.Equals(descriptor.Name, applicationStep.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Catalog step '{descriptor.Name}' does not match configured step '{applicationStep.Name}'.");
        }

        if (!string.Equals(descriptor.Provider, AzurePipelinesProvider, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Catalog step '{descriptor.Name}' is not declared for Azure Pipelines.");
        }

        if (!descriptor.Template.StartsWith(TemplatePrefix, StringComparison.Ordinal)
            || descriptor.Template.Contains("..", StringComparison.Ordinal)
            || descriptor.Template.Contains('\\'))
        {
            throw new InvalidOperationException($"Catalog step '{descriptor.Name}' has an invalid Azure Pipelines template path.");
        }

        var configured = applicationStep.Parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Value,
            StringComparer.Ordinal);
        var parameters = descriptor.Parameters
            .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
            .Select(parameter => TranslateParameter(descriptor.Name, parameter, configured))
            .Where(parameter => parameter is not null)
            .Select(parameter => parameter!)
            .ToArray();
        var unknown = configured.Keys.Except(descriptor.Parameters.Select(parameter => parameter.Name), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (unknown.Length > 0)
        {
            throw new InvalidOperationException($"Catalog step '{descriptor.Name}' does not declare parameter '{unknown[0]}'.");
        }

        return new PipelineStepTranslation(applicationStep.Name, applicationStep.Blocking, applicationStep.Position, descriptor.Template, parameters);
    }

    private static PipelineStepParameterTranslation? TranslateParameter(
        string stepName,
        CatalogPipelineStepDescriptor.ParameterDescriptor descriptor,
        IReadOnlyDictionary<string, string> configuredParameters)
    {
        var configured = configuredParameters.GetValueOrDefault(descriptor.Name);
        var value = string.IsNullOrWhiteSpace(configured) ? descriptor.DefaultValue : configured;
        if (value is null)
        {
            throw new InvalidOperationException($"Catalog step '{stepName}' requires parameter '{descriptor.Name}'.");
        }

        if (descriptor.Values.Length > 0 && !descriptor.Values.Contains(value, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Value '{value}' is not supported for catalog step '{stepName}' parameter '{descriptor.Name}'.");
        }

        return new PipelineStepParameterTranslation(descriptor.Name, value);
    }
}
