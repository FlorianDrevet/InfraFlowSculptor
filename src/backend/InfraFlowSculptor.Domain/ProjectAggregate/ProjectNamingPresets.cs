namespace InfraFlowSculptor.Domain.ProjectAggregate;

public static class ProjectNamingPresets
{
    public const string Compact = "Compact";
    public const string Caf = "CAF";
    public const string Custom = "Custom";

    public const string CompactDefaultTemplate = "{abbr}-{project}-{name}-{env}";
    public const string CompactResourceGroupTemplate = "rg-{project}-{component}-{name}-{env}";
    public const string CafDefaultTemplate = "{abbr}-{project}-{name}-{env}-{region}";
    public const string CafResourceGroupTemplate = "rg-{project}-{component}-{name}-{env}-{region}";
}
