using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Application.Generation;

public sealed class StepTranslatorRegistry
{
    private readonly Registry<CiPlatform, IStepTranslator> registry;

    public StepTranslatorRegistry(IEnumerable<IStepTranslator> translators) =>
        registry = new Registry<CiPlatform, IStepTranslator>(translators);

    public IReadOnlyCollection<CiPlatform> Keys => registry.Keys;

    public bool Supports(CiPlatform key) => registry.Supports(key);

    public IStepTranslator Get(CiPlatform key) => registry.Get(key);
}
