using System.Security.Cryptography;
using System.Text;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.Invitations;

namespace InfraFlowSculptor.Domain.Tests.Organizations;

public sealed class InvitationAggregateTests
{
    private static readonly OrganizationRole[] Roles = [OrganizationRole.Member];
    private const string Token = "a-256-bit-random-url-safe-token-value";

    [Fact]
    public void CreateStoresOnlyTheTokenHashAndRaisesInvitationCreated()
    {
        var now = DateTimeOffset.Parse("2026-10-27T12:00:00Z");

        var invitation = InvitationAggregate.Create(
            new InvitationId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            "Nina@contoso.example",
            Roles,
            Token,
            "fr",
            now);

        Assert.False(invitation.IsError);
        Assert.Equal("nina@contoso.example", invitation.Value.Email);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Token))), invitation.Value.TokenHash);
        Assert.DoesNotContain(Token, invitation.Value.TokenHash, StringComparison.Ordinal);
        Assert.Equal(now.AddDays(7), invitation.Value.ExpiresAt);
        var created = Assert.IsType<InvitationCreated>(Assert.Single(invitation.Value.DomainEvents));
        Assert.Equal(Token, created.Token);
        Assert.Null(typeof(InvitationAggregate).GetProperty("Token"));
    }

    [Fact]
    public void AcceptRequiresMatchingVerifiedEmailAndAcceptsCaseInsensitiveMatch()
    {
        var now = DateTimeOffset.Parse("2026-10-27T12:00:00Z");
        var invitation = CreateInvitation(now);

        var accepted = invitation.Accept("NINA@CONTOSO.EXAMPLE", now.AddMinutes(1));

        Assert.False(accepted.IsError);
        Assert.Equal(InvitationStatus.Accepted, invitation.StatusAt(now.AddMinutes(1)));
    }

    [Fact]
    public void AcceptingAgainWithTheSameVerifiedEmailIsIdempotent()
    {
        var now = DateTimeOffset.Parse("2026-10-27T12:00:00Z");
        var invitation = CreateInvitation(now);

        Assert.False(invitation.Accept("nina@contoso.example", now.AddMinutes(1)).IsError);

        var acceptedAgain = invitation.Accept("NINA@CONTOSO.EXAMPLE", now.AddMinutes(2));

        Assert.False(acceptedAgain.IsError);
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
    }

    [Fact]
    public void AcceptRejectsUnverifiedOrMismatchedEmail()
    {
        var now = DateTimeOffset.Parse("2026-10-27T12:00:00Z");
        var unverifiedInvitation = CreateInvitation(now);
        var mismatchInvitation = CreateInvitation(now);

        var unverified = unverifiedInvitation.Accept(null, now.AddMinutes(1));
        var mismatch = mismatchInvitation.Accept("emma@outlook.example", now.AddMinutes(1));

        Assert.Equal("INVITATION_EMAIL_NOT_VERIFIED", unverified.FirstError.Code);
        Assert.Equal("INVITATION_EMAIL_MISMATCH", mismatch.FirstError.Code);
        Assert.Equal(InvitationStatus.Pending, unverifiedInvitation.Status);
        Assert.Equal(InvitationStatus.Pending, mismatchInvitation.Status);
    }

    [Fact]
    public void ExpiredInvitationCannotBeAcceptedAndRevokeIsTerminal()
    {
        var now = DateTimeOffset.Parse("2026-10-27T12:00:00Z");
        var expiredInvitation = CreateInvitation(now);
        var revokedInvitation = CreateInvitation(now);
        Assert.False(revokedInvitation.Revoke(now.AddMinutes(1)).IsError);

        var expired = expiredInvitation.Accept("nina@contoso.example", now.AddDays(7));
        var revoked = revokedInvitation.Accept("nina@contoso.example", now.AddMinutes(2));

        Assert.Equal("INVITATION_EXPIRED", expired.FirstError.Code);
        Assert.Equal(InvitationStatus.Expired, expiredInvitation.StatusAt(now.AddDays(7)));
        Assert.Equal("INVITATION_REVOKED", revoked.FirstError.Code);
    }

    private static InvitationAggregate CreateInvitation(DateTimeOffset now) =>
        InvitationAggregate.Create(
            new InvitationId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            "nina@contoso.example",
            Roles,
            Token,
            "fr",
            now).Value;
}
