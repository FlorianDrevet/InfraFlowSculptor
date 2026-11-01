using InfraFlowSculptor.Domain.Common.Extensibility;

namespace InfraFlowSculptor.Engine.Exposure;

public sealed class ExposureHandlerRegistry
{
    private readonly Registry<ExposureMode, IExposureHandler> registry;

    public ExposureHandlerRegistry(IEnumerable<IExposureHandler> handlers) =>
        registry = new Registry<ExposureMode, IExposureHandler>(handlers);

    public IReadOnlyCollection<ExposureMode> Keys => registry.Keys;

    public bool Supports(ExposureMode mode) => registry.Supports(mode);

    public IExposureHandler Get(ExposureMode mode) => registry.Get(mode);
}
