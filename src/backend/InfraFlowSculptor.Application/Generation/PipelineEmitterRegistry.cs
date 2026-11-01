using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Application.Generation;

public sealed class PipelineEmitterRegistry
{
    private readonly Registry<CiPlatform, IPipelineEmitter> registry;

    public PipelineEmitterRegistry(IEnumerable<IPipelineEmitter> emitters) =>
        registry = new Registry<CiPlatform, IPipelineEmitter>(emitters);

    public IReadOnlyCollection<CiPlatform> Keys => registry.Keys;

    public bool Supports(CiPlatform key) => registry.Supports(key);

    public IPipelineEmitter Get(CiPlatform key) => registry.Get(key);
}
