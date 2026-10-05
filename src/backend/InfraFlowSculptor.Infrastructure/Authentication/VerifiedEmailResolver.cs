using System.Security.Claims;
using InfraFlowSculptor.Application.Common.Security;

namespace InfraFlowSculptor.Infrastructure.Authentication;

public sealed class VerifiedEmailResolver : IVerifiedEmailResolver
{
    public VerifiedEmail? Resolve(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var email = ReadNonEmptyClaim(principal, AuthenticationClaims.Email);
        var verifiedPrimaryEmail = ReadNonEmptyClaim(principal, AuthenticationClaims.VerifiedPrimaryEmail);

        if (verifiedPrimaryEmail is not null)
        {
            return new VerifiedEmail(verifiedPrimaryEmail);
        }

        if (HasTrueClaim(principal, AuthenticationClaims.EntraEmailVerified) && email is not null)
        {
            return new VerifiedEmail(email);
        }

        if (IsPersonalAccount(principal) && email is not null)
        {
            return new VerifiedEmail(email);
        }

        if (HasTrueClaim(principal, AuthenticationClaims.EmailVerified) && email is not null)
        {
            return new VerifiedEmail(email);
        }

        return null;
    }

    private static bool IsPersonalAccount(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(AuthenticationClaims.TenantId)?.Value, out var tenantId)
        && tenantId == Guid.Parse(EntraConstants.PersonalAccountsTenantId);

    private static bool HasTrueClaim(ClaimsPrincipal principal, string claimType) =>
        bool.TryParse(principal.FindFirst(claimType)?.Value, out var isTrue) && isTrue;

    private static string? ReadNonEmptyClaim(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirst(claimType)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
