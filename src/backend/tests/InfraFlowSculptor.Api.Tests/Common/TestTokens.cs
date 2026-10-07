using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace InfraFlowSculptor.Api.Tests.Common;

internal static class TestTokens
{
    public const string Audience = "ifs-api-tests";
    public const string Issuer = "ifs-tests";
    public const string SigningKey = "ThisIsOnlyATestSigningKeyForInfraFlowSculptor";

    public static string CreateAccessToken(
        string tenantId = "11111111-1111-1111-1111-111111111111",
        string objectId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
        string displayName = "Alice Martin",
        string email = "alice@contoso.example",
        bool emailVerified = true,
        string? verifiedPrimaryEmail = null,
        bool? entraEmailVerified = null)
    {
        var claims = new List<Claim>
        {
            new("tid", tenantId),
            new("oid", objectId),
            new("name", displayName),
            new("email", email),
            new("email_verified", emailVerified.ToString(), ClaimValueTypes.Boolean)
        };

        if (verifiedPrimaryEmail is not null)
        {
            claims.Add(new Claim("verified_primary_email", verifiedPrimaryEmail));
        }

        if (entraEmailVerified is not null)
        {
            claims.Add(new Claim("xms_edov", entraEmailVerified.Value.ToString(), ClaimValueTypes.Boolean));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
