namespace InfraFlowSculptor.Application.Common.Extensibility;

public sealed class Registry<TKey, TService> where TKey : notnull
    where TService : IKeyed<TKey>
{
    private readonly Dictionary<TKey, TService> _services;

    public Registry(IEnumerable<TService> services)
    {
        _services = services.ToDictionary(service => service.Key);
    }

    public IReadOnlyCollection<TKey> Keys => _services.Keys;

    public bool Supports(TKey key) => _services.ContainsKey(key);

    public TService Get(TKey key)
    {
        return _services.TryGetValue(key, out var service)
            ? service
            : throw new KeyNotFoundException($"Key '{key}' is not supported.");
    }
}
