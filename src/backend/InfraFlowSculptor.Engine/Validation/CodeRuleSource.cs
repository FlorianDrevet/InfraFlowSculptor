namespace InfraFlowSculptor.Engine.Validation;

public sealed class CodeRuleSource(IEnumerable<IValidationRule> rules) : IRuleSource
{
    public string Key => "code";

    public IReadOnlyList<IValidationRule> GetRules() => rules
        .OrderBy(rule => rule.Code, StringComparer.Ordinal)
        .ToArray();
}
