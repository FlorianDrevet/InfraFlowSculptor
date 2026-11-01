using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.Bicep;

public sealed class BicepInfrastructureEmitter : IInfrastructureEmitter
{
    private const string SupportedModuleSourceName = "AVM registre public";
    private readonly ModuleSourceStrategyRegistry moduleSources;

    public BicepInfrastructureEmitter(ModuleSourceStrategyRegistry moduleSources)
    {
        ArgumentNullException.ThrowIfNull(moduleSources);
        this.moduleSources = moduleSources;
    }

    public EmitterKey Key => new(IacLanguage.Bicep);

    public IReadOnlyList<GeneratedFile> Emit(DeploymentPlan plan, CatalogVersion catalog)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(catalog);
        if (!string.Equals(plan.Project.InfrastructureLanguage, nameof(IacLanguage.Bicep), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Bicep generation does not support infrastructure language '{plan.Project.InfrastructureLanguage}'.");
        }

        if (!string.Equals(plan.Project.ModuleSource, SupportedModuleSourceName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Bicep generation does not support module source '{plan.Project.ModuleSource}'.");
        }

        if (!string.Equals(plan.Pins.Version, catalog.Version, StringComparison.Ordinal)
            || !string.Equals(plan.Pins.Fingerprint, catalog.Fingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The deployment plan and catalog version or fingerprint do not match.");
        }

        BicepCatalogSupportValidator.Validate(plan, catalog);

        var generated = new List<GeneratedFile>();
        foreach (var component in plan.Components)
        {
            generated.Add(TypesFileEmitter.Emit(plan, component, catalog));
            generated.Add(MainFileEmitter.Emit(plan, component, catalog, moduleSources));
            generated.AddRange(MainFileEmitter.EmitHelpers(plan, component, catalog));

            foreach (var resource in component.Targets.SelectMany(target => target.Resources)
                .Where(resource => !resource.IsExisting)
                .DistinctBy(resource => resource.Type, StringComparer.Ordinal)
                .Where(resource =>
                {
                    var descriptor = catalog.GetResourceType(resource.Type)
                        ?? throw new InvalidOperationException(
                            $"The pinned catalog has no descriptor for resource type '{resource.Type}'.");
                    return !BicepCatalogMetadata.IsEmbedded(descriptor);
                }))
            {
                var descriptor = catalog.GetResourceType(resource.Type)
                    ?? throw new InvalidOperationException(
                        $"The pinned catalog has no descriptor for resource type '{resource.Type}'.");
                _ = moduleSources.Get(ModuleSource.PublicAvmRegistry).GetModuleReference(plan, descriptor);
            }

            foreach (var target in component.Targets)
            {
                generated.Add(ParamFileEmitter.Emit(plan, component, target, catalog));
                generated.Add(ReleaseDataEmitter.Emit(component, target));
            }

            foreach (var script in DataAccessScriptEmitter.Emit(component))
            {
                generated.Add(new GeneratedFile(
                    $"{component.ComponentCode}/infra/{script.RelativePath}",
                    script.Content));
            }
        }

        var duplicatePath = generated
            .GroupBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePath is not null)
        {
            throw new InvalidOperationException(
                $"Bicep catalog metadata generated duplicate output path '{duplicatePath.Key}'.");
        }

        return generated
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }
}
