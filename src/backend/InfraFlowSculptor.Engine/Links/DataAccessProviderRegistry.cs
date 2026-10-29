using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed class DataAccessProviderRegistry
{
    private readonly Registry<DataEngine, IDataAccessProvider> registry;

    public DataAccessProviderRegistry(IEnumerable<IDataAccessProvider> providers) =>
        registry = new Registry<DataEngine, IDataAccessProvider>(providers);

    public IReadOnlyCollection<DataEngine> Keys => registry.Keys;

    public IDataAccessProvider Get(DataEngine engine) => registry.Get(engine);

    public IDataAccessProvider? FindByCatalogEngineName(string catalogEngineName) => Keys
        .Select(registry.Get)
        .FirstOrDefault(provider => string.Equals(
            provider.CatalogEngineName,
            catalogEngineName,
            StringComparison.OrdinalIgnoreCase));
}
