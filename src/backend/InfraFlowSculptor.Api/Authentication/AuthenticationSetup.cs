using System.Text;
using InfraFlowSculptor.Application.Common.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;

namespace InfraFlowSculptor.Api.Authentication;

public static class AuthenticationSetup
{
    public static IServiceCollection AddIfsAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var authSection = configuration.GetSection(AuthOptions.SectionName);
        var authOptions = authSection.Get<AuthOptions>() ?? new AuthOptions();
        var testSigningKey = configuration[AuthOptions.TestSigningKeyConfigurationKey];

        if (testSigningKey is not null && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                $"{AuthOptions.TestSigningKeyConfigurationKey} is only allowed in the Testing environment.");
        }

        if (!string.IsNullOrWhiteSpace(testSigningKey) && Encoding.UTF8.GetByteCount(testSigningKey) < 32)
        {
            throw new InvalidOperationException(
                $"{AuthOptions.TestSigningKeyConfigurationKey} must contain at least 32 bytes.");
        }

        services.AddOptions<AuthOptions>()
            .Bind(authSection)
            .Validate(options => options.Provider is AuthProvider.Keycloak or AuthProvider.Entra,
                "Auth:Provider must be Keycloak or Entra.")
            .Validate(options => Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority)
                && authority.Scheme == Uri.UriSchemeHttps,
                "Auth:Authority must be an absolute HTTPS URL.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Auth:Audience is required.")
            .Validate(options => options.Provider != AuthProvider.Entra
                || !string.IsNullOrWhiteSpace(options.TenantId),
                "Auth:TenantId is required for Entra.")
            .ValidateOnStart();

        var authentication = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = Schemes.Bearer;
            options.DefaultChallengeScheme = Schemes.Bearer;
            options.DefaultScheme = Schemes.Bearer;
        });

        authentication.AddPolicyScheme(Schemes.Bearer, Schemes.Bearer, options =>
        {
            options.ForwardDefaultSelector = context =>
                context.Request.Headers.Authorization
                    .ToString()
                    .StartsWith(Schemes.ApiTokenAuthorizationPrefix, StringComparison.OrdinalIgnoreCase)
                    ? Schemes.ApiToken
                    : Schemes.Oidc;
        });

        if (environment.IsEnvironment("Testing") && !string.IsNullOrWhiteSpace(testSigningKey))
        {
            authentication.AddJwtBearer(Schemes.Oidc, options =>
                ConfigureTestJwt(options, testSigningKey, authOptions.Audience!));
        }
        else if (authOptions.Provider == AuthProvider.Keycloak)
        {
            authentication.AddJwtBearer(Schemes.Oidc, options =>
                ConfigureJwt(options, authOptions, environment));
        }
        else if (authOptions.Provider == AuthProvider.Entra)
        {
            authentication.AddMicrosoftIdentityWebApi(authSection, Schemes.Oidc);
            services.Configure<JwtBearerOptions>(Schemes.Oidc, options =>
                ConfigureCommonJwtOptions(options, environment));
        }
        else
        {
            throw new InvalidOperationException("Auth:Provider must be Keycloak or Entra.");
        }

        authentication.AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(
            Schemes.ApiToken,
            _ => { });

        return services;
    }

    private static void ConfigureJwt(
        JwtBearerOptions options,
        AuthOptions authOptions,
        IHostEnvironment environment)
    {
        ConfigureCommonJwtOptions(options, environment);
        options.Authority = authOptions.Authority!;
        options.Audience = authOptions.Audience!;
    }

    private static void ConfigureCommonJwtOptions(JwtBearerOptions options, IHostEnvironment environment)
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !environment.IsDevelopment();
        options.TokenValidationParameters.NameClaimType = AuthenticationClaims.DisplayName;
        options.TokenValidationParameters.RoleClaimType = AuthenticationClaims.Roles;
        options.TokenValidationParameters.AuthenticationType = Schemes.Oidc;
    }

    private static void ConfigureTestJwt(JwtBearerOptions options, string signingKey, string audience)
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            NameClaimType = AuthenticationClaims.DisplayName,
            RoleClaimType = AuthenticationClaims.Roles,
            AuthenticationType = Schemes.Oidc
        };
    }
}
