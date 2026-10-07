namespace InfraFlowSculptor.Api.Common;

public static class RateLimitingPolicies
{
    public const string Read = "read";
    public const string Write = "write";
    public const string Generate = "generate";
    public const string Publish = "publish";
}
