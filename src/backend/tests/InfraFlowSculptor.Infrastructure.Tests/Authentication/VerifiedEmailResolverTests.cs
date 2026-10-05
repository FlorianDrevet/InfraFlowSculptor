using System.Security.Claims;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Infrastructure.Authentication;

namespace InfraFlowSculptor.Infrastructure.Tests.Authentication;

public sealed class VerifiedEmailResolverTests
{
    private readonly VerifiedEmailResolver resolver = new();

    [Fact]
    public void ResolveWithVerifiedPrimaryEmailReturnsThatAddress()
    {
        var principal = CreatePrincipal(
            new Claim(AuthenticationClaims.Email, "other@contoso.example"),
            new Claim(AuthenticationClaims.VerifiedPrimaryEmail, "primary@contoso.example"));

        Assert.Equal(new VerifiedEmail("primary@contoso.example"), resolver.Resolve(principal));
    }

    [Fact]
    public void ResolveWithEntraEmailVerifiedReturnsEmail()
    {
        var principal = CreatePrincipal(
            new Claim(AuthenticationClaims.Email, "entra@contoso.example"),
            new Claim(AuthenticationClaims.EntraEmailVerified, "true"));

        Assert.Equal(new VerifiedEmail("entra@contoso.example"), resolver.Resolve(principal));
    }

    [Fact]
    public void ResolveWithPersonalAccountTenantReturnsEmail()
    {
        var principal = CreatePrincipal(
            new Claim(AuthenticationClaims.Email, "personal@outlook.example"),
            new Claim(AuthenticationClaims.TenantId, EntraConstants.PersonalAccountsTenantId));

        Assert.Equal(new VerifiedEmail("personal@outlook.example"), resolver.Resolve(principal));
    }

    [Fact]
    public void ResolveWithKeycloakVerifiedEmailReturnsEmail()
    {
        var principal = CreatePrincipal(
            new Claim(AuthenticationClaims.Email, "alice@contoso.example"),
            new Claim(AuthenticationClaims.EmailVerified, "true"));

        Assert.Equal(new VerifiedEmail("alice@contoso.example"), resolver.Resolve(principal));
    }

    [Fact]
    public void ResolvePrefersVerifiedPrimaryEmailOverOtherRules()
    {
        var principal = CreatePrincipal(
            new Claim(AuthenticationClaims.Email, "email@contoso.example"),
            new Claim(AuthenticationClaims.VerifiedPrimaryEmail, "primary@contoso.example"),
            new Claim(AuthenticationClaims.EntraEmailVerified, "true"),
            new Claim(AuthenticationClaims.TenantId, EntraConstants.PersonalAccountsTenantId),
            new Claim(AuthenticationClaims.EmailVerified, "true"));

        Assert.Equal(new VerifiedEmail("primary@contoso.example"), resolver.Resolve(principal));
    }

    [Fact]
    public void ResolveWithUnverifiedEmailReturnsNull()
    {
        var principal = CreatePrincipal(new Claim(AuthenticationClaims.Email, "nina@contoso.example"));

        Assert.Null(resolver.Resolve(principal));
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims));
}
