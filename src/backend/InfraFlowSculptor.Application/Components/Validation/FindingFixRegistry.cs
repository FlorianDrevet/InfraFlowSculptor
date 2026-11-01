using InfraFlowSculptor.Domain.Common.Extensibility;

namespace InfraFlowSculptor.Application.Components.Validation;

public sealed class FindingFixRegistry
{
    private readonly Registry<string, IFindingFix> registry;

    public FindingFixRegistry(IEnumerable<IFindingFix> fixes) =>
        registry = new Registry<string, IFindingFix>(fixes);

    public bool Supports(string code) => registry.Supports(code);

    public IFindingFix Get(string code) => registry.Get(code);
}
