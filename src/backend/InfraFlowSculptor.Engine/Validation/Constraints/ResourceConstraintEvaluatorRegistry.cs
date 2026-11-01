using InfraFlowSculptor.Domain.Common.Extensibility;

namespace InfraFlowSculptor.Engine.Validation.Constraints;

public sealed class ResourceConstraintEvaluatorRegistry
{
    private readonly Registry<string, IResourceConstraintEvaluator> registry;

    public ResourceConstraintEvaluatorRegistry(IEnumerable<IResourceConstraintEvaluator> evaluators)
    {
        ArgumentNullException.ThrowIfNull(evaluators);
        registry = new Registry<string, IResourceConstraintEvaluator>(evaluators);
    }

    public static ResourceConstraintEvaluatorRegistry CreateDefault() => new(
    [
        new CompareResourceConstraintEvaluator(),
        new AllowedTuplesResourceConstraintEvaluator()
    ]);

    public IReadOnlyCollection<string> Keys => registry.Keys;

    public bool TryGet(string key, out IResourceConstraintEvaluator evaluator)
    {
        if (registry.Supports(key))
        {
            evaluator = registry.Get(key);
            return true;
        }

        evaluator = null!;
        return false;
    }
}
