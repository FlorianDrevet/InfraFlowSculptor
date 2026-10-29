namespace InfraFlowSculptor.Domain.Common.Extensibility;

public sealed class Registry<TKey, TService> where TKey : notnull
    where TService : IKeyed<TKey>
{
    private readonly Dictionary<TKey, TService> services;

    public Registry(IEnumerable<TService> services)
    {
        this.services = services.ToDictionary(service => service.Key);
    }

    public IReadOnlyCollection<TKey> Keys => services.Keys;

    public bool Supports(TKey key) => services.ContainsKey(key);

    public TService Get(TKey key) => services.TryGetValue(key, out var service)
        ? service
        : throw new KeyNotFoundException($"Key '{key}' is not supported.");
}
