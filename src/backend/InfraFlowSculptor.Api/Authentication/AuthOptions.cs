namespace InfraFlowSculptor.Api.Authentication;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public const string AuthorityConfigurationKey = "Auth:Authority";
    public const string TestSigningKeyConfigurationKey = "Auth:TestSigningKey";
    public const string LocalKeycloakAuthority = "https://localhost:8080/realms/ifs";

    public AuthProvider Provider { get; set; }

    public string? Authority { get; set; }

    public string? Audience { get; set; }

    public string? TenantId { get; set; }
}
