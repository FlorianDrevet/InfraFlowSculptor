using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Application.Generation;

public sealed class InstallKitPlatformEmitterRegistry
{
    private readonly Registry<CiPlatform, IInstallKitPlatformEmitter> registry;

    public InstallKitPlatformEmitterRegistry(IEnumerable<IInstallKitPlatformEmitter> emitters) =>
        registry = new Registry<CiPlatform, IInstallKitPlatformEmitter>(emitters);

    public IReadOnlyCollection<CiPlatform> Keys => registry.Keys;

    public bool Supports(CiPlatform key) => registry.Supports(key);

    public IInstallKitPlatformEmitter Get(CiPlatform key) => registry.Get(key);
}
