namespace InfraFlowSculptor.Api.Configuration;

public static class ScalarOAuth2
{
    public const string SecuritySchemeName = "OAuth2";
    public const string ClientId = "ifs-scalar";
    public const string OpenIdScope = "openid";
    public const string ProfileScope = "profile";
    public const string EmailScope = "email";
    public static readonly string[] Scopes = [OpenIdScope, ProfileScope, EmailScope];

    public static string AuthorizationUrl(string authority) =>
        $"{authority.TrimEnd('/')}/protocol/openid-connect/auth";

    public static string TokenUrl(string authority) =>
        $"{authority.TrimEnd('/')}/protocol/openid-connect/token";
}
