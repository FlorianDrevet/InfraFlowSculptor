using InfraFlowSculptor.Domain.Common.Extensibility;

namespace InfraFlowSculptor.Engine.Validation;

public interface IRuleSource : IKeyed<string>
{
    IReadOnlyList<IValidationRule> GetRules();
}
