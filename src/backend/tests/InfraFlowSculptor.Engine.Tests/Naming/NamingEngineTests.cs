using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;

namespace InfraFlowSculptor.Engine.Tests.Naming;

public sealed class NamingEngineTests
{
    [Fact]
    public void CompactPresetPreviewsResourceAndResourceGroupNames()
    {
        var result = NamingEngine.Preview(CreateSnapshot(Compact()));

        Assert.Empty(result.Issues);
        var preview = Assert.Single(result.Items);
        Assert.Equal("kv-shop-main-dev", preview.ResourceName);
        Assert.Equal("rg-shop-main-main-dev", preview.ResourceGroupName);
    }

    [Fact]
    public void CafPresetAddsTheEnvironmentRegionToBothNames()
    {
        var result = NamingEngine.Preview(CreateSnapshot(Caf()));

        Assert.Empty(result.Issues);
        var preview = Assert.Single(result.Items);
        Assert.Equal("kv-shop-main-dev-francecentral", preview.ResourceName);
        Assert.Equal("rg-shop-main-main-dev-francecentral", preview.ResourceGroupName);
    }

    [Fact]
    public void PreviewReportsUnknownTokensAndSupportedTokenNames()
    {
        var custom = new NamingConventionSnapshot(
            "Custom",
            "{abbr}-{project}-{name}-{zone}",
            "rg-{project}-{component}-{name}-{env}");

        var result = NamingEngine.Preview(CreateSnapshot(custom));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("NAMING_TOKEN_UNKNOWN", issue.Code);
        Assert.Contains("region", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreviewRequiresNameTokenInEachTemplate()
    {
        var custom = new NamingConventionSnapshot("Custom", "{abbr}-{project}-{env}", "rg-{project}-{component}-{env}");

        var result = NamingEngine.Preview(CreateSnapshot(custom));

        Assert.Equal(2, result.Issues.Count(issue => issue.Code == "NAMING_NAME_TOKEN_REQUIRED"));
    }

    [Fact]
    public void PreviewDoesNotSanitizeRenderedValues()
    {
        var snapshot = CreateSnapshot(new NamingConventionSnapshot(
            "Custom",
            "{name}-{region}",
            "rg-{name}-{region}"), region: "West US");

        var result = NamingEngine.Preview(snapshot);

        Assert.Empty(result.Issues);
        Assert.Equal("main-West US", Assert.Single(result.Items).ResourceName);
    }

    private static ModelSnapshot CreateSnapshot(NamingConventionSnapshot naming, string region = "francecentral") => new(
        new ProjectSnapshot("Boutique en ligne", "shop", string.Empty),
        [new EnvironmentSnapshot("dev", region)],
        naming,
        new Dictionary<string, string> { ["costCenter"] = "commerce" });

    private static NamingConventionSnapshot Compact() => new(
        "Compact",
        "{abbr}-{project}-{name}-{env}",
        "rg-{project}-{component}-{name}-{env}");

    private static NamingConventionSnapshot Caf() => new(
        "CAF",
        "{abbr}-{project}-{name}-{env}-{region}",
        "rg-{project}-{component}-{name}-{env}-{region}");
}
