using System.Security.Claims;
using InfraFlowSculptor.Application.Common.Security;
using Microsoft.AspNetCore.Http;

namespace InfraFlowSculptor.Infrastructure.Authentication;

public sealed class HttpCurrentUser(
    IHttpContextAccessor httpContextAccessor,
    IVerifiedEmailResolver verifiedEmailResolver) : ICurrentUser
{
    private ClaimsPrincipal Principal => httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();

    private ClaimsIdentity? AuthenticatedIdentity => Principal.Identities.FirstOrDefault(identity => identity.IsAuthenticated);

    public UserKey Key => new(
        ReadGuidClaim(AuthenticationClaims.TenantId),
        ReadGuidClaim(AuthenticationClaims.ObjectId));

    public string DisplayName =>
        Principal.FindFirst(AuthenticationClaims.DisplayName)?.Value
        ?? AuthenticatedIdentity?.Name
        ?? string.Empty;

    public VerifiedEmail? VerifiedEmail => verifiedEmailResolver.Resolve(Principal);

    public bool IsAuthenticated => AuthenticatedIdentity is not null;

    public AuthenticationKind AuthenticationKind =>
        AuthenticatedIdentity?.AuthenticationType == Schemes.ApiToken
            ? AuthenticationKind.ApiToken
            : AuthenticationKind.Oidc;

    private Guid ReadGuidClaim(string claimType) =>
        Guid.TryParse(Principal.FindFirst(claimType)?.Value, out var value) ? value : Guid.Empty;
}
