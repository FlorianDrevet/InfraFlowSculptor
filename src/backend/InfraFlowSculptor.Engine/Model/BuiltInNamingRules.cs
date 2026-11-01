namespace InfraFlowSculptor.Engine.Model;

public static class BuiltInNamingRules
{
    public const string ResourceGroupType = "ResourceGroup";

    public static NamingRuleSnapshot ResourceGroup { get; } = new(
        "rg",
        @"^[-\w._()]{1,90}$",
        "mixed",
        ["-", "_", "."],
        false,
        1,
        90,
        "any",
        "notDot",
        "subscription",
        null);
}
