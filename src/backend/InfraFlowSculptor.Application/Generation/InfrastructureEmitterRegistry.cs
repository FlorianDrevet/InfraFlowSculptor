using InfraFlowSculptor.Domain.Common.Extensibility;

namespace InfraFlowSculptor.Application.Generation;

public sealed class InfrastructureEmitterRegistry
{
    private readonly Registry<EmitterKey, IInfrastructureEmitter> registry;

    public InfrastructureEmitterRegistry(IEnumerable<IInfrastructureEmitter> emitters) =>
        registry = new Registry<EmitterKey, IInfrastructureEmitter>(emitters);

    public IReadOnlyCollection<EmitterKey> Keys => registry.Keys;

    public bool Supports(EmitterKey key) => registry.Supports(key);

    public IInfrastructureEmitter Get(EmitterKey key) => registry.Get(key);
}
