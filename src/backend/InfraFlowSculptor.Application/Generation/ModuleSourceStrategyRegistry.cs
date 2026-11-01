using InfraFlowSculptor.Domain.Common.Extensibility;

namespace InfraFlowSculptor.Application.Generation;

public sealed class ModuleSourceStrategyRegistry
{
    private readonly Registry<ModuleSource, IModuleSourceStrategy> registry;

    public ModuleSourceStrategyRegistry(IEnumerable<IModuleSourceStrategy> strategies) =>
        registry = new Registry<ModuleSource, IModuleSourceStrategy>(strategies);

    public IReadOnlyCollection<ModuleSource> Keys => registry.Keys;

    public bool Supports(ModuleSource key) => registry.Supports(key);

    public IModuleSourceStrategy Get(ModuleSource key) => registry.Get(key);
}
