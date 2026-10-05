using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace InfraFlowSculptor.Architecture.Tests;

public sealed class NoSwitchOnExtensionKeysTests
{
    [Fact]
    public void ProductionCodeDoesNotSwitchOrCompareExtensionKeyEnums()
    {
        if (ExtensionKeys.Types.Count == 0)
        {
            return;
        }

        var violations = ExtensionKeyRules.FindSwitchOrComparisonViolations(
            ProductionSources.CreateCompilation(),
            ExtensionKeys.Types);

        Assert.Empty(violations);
    }

    [Fact]
    public void RuleDetectsSwitchesAndComparisonsOnExtensionKeyEnums()
    {
        const string source = """
            using InfraFlowSculptor.Architecture.Tests;

            internal static class ExtensionKeyRuleProbe
            {
                internal static int SwitchOnKey(SwitchRuleProbeKey key) => key switch
                {
                    SwitchRuleProbeKey.First => 1,
                    _ => 0
                };

                internal static bool CompareKey(SwitchRuleProbeKey key) => key == SwitchRuleProbeKey.Second;
            }
            """;
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "InfraFlowSculptor.ExtensionKeyRuleProbe",
            [syntaxTree],
            ProductionSources.MetadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var violations = ExtensionKeyRules.FindSwitchOrComparisonViolations(
            compilation,
            [typeof(SwitchRuleProbeKey)]);

        Assert.Equal(2, violations.Count);
    }
}
