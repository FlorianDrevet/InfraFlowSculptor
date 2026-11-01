using InfraFlowSculptor.Domain.Common.Extensibility;

namespace InfraFlowSculptor.Application.Generation;

public sealed class OutputCheckerRegistry
{
    private readonly Registry<IacLanguage, IOutputChecker> registry;

    public OutputCheckerRegistry(IEnumerable<IOutputChecker> checkers) =>
        registry = new Registry<IacLanguage, IOutputChecker>(checkers);

    public IReadOnlyCollection<IacLanguage> Keys => registry.Keys;

    public bool Supports(IacLanguage key) => registry.Supports(key);

    public IOutputChecker Get(IacLanguage key) => registry.Get(key);
}
