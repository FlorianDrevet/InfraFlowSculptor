using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;

namespace InfraFlowSculptor.Engine.Tests.Naming;

public sealed class NameSanitizerTests
{
    private static readonly NamingRuleSnapshot KeyVault = new(
        "kv",
        "^[a-zA-Z][a-zA-Z0-9-]{1,22}[a-zA-Z0-9]$",
        "lower",
        ["-"],
        true,
        3,
        24,
        "letter",
        "alphanumeric",
        "global",
        "vault.azure.net");

    [Fact]
    public void SanitizeAppliesCaseRemovalCollapseAndBoundaryRulesInOrder()
    {
        var result = NameSanitizer.Sanitize("-API--SERVICE-", KeyVault);

        Assert.Equal("api-service", result.Value);
        Assert.Collection(
            result.Transformations,
            transformation => Assert.Equal("Casse mise en minuscules", transformation),
            transformation => Assert.Equal("Séparateurs consécutifs réduits", transformation),
            transformation => Assert.Equal("Caractères de bord retirés", transformation));
    }

    [Fact]
    public void ForcedNamesMustNotContainConsecutiveSeparators()
    {
        Assert.False(NameSanitizer.IsValid("vault--01", KeyVault));
        Assert.True(NameSanitizer.IsValid("vault-01", KeyVault));
    }

    [Fact]
    public void ForcedNamesMustRespectTheDeclaredCase()
    {
        Assert.False(NameSanitizer.IsValid("Vault-01", KeyVault));
        Assert.True(NameSanitizer.IsValid("vault-01", KeyVault));
    }
}
