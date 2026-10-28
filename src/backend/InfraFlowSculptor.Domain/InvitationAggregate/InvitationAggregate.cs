using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Domain.Invitations;

[AuditedObject(ObjectType.Invitation, nameof(InvitationAggregate.Id), nameof(InvitationAggregate.OrganizationId))]
public sealed class InvitationAggregate : AggregateRoot<InvitationId>, IOrganizationOwned, IHasVersion
{
    public const int ValidityDays = 7;

    private int[] roleValues = [];

    public OrganizationId OrganizationId { get; private set; }

    [PersonalData("email")]
    public string Email { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public IReadOnlyCollection<OrganizationRole> Roles =>
        roleValues.Select(value => (OrganizationRole)value).ToArray();

    public int[] RoleValues
    {
        get => roleValues;
        private set => roleValues = value;
    }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public InvitationStatus Status { get; private set; }

    public string Language { get; private set; } = string.Empty;

    public int Version { get; private set; } = 1;

    private InvitationAggregate()
    {
    }

    private InvitationAggregate(
        InvitationId id,
        OrganizationId organizationId,
        string email,
        IEnumerable<OrganizationRole> roles,
        string token,
        string language,
        DateTimeOffset now) : base(id)
    {
        OrganizationId = organizationId;
        Email = email;
        roleValues = roles.Select(role => (int)role).ToArray();
        TokenHash = ComputeTokenHash(token);
        Language = language;
        CreatedAt = now;
        ExpiresAt = now.AddDays(ValidityDays);
        Status = InvitationStatus.Pending;
        RaiseInvitationCreated(token);
    }

    public static ErrorOr<InvitationAggregate> Create(
        InvitationId id,
        OrganizationId organizationId,
        string email,
        IEnumerable<OrganizationRole> roles,
        string token,
        string language,
        DateTimeOffset now)
    {
        if (id.Value == Guid.Empty || organizationId.Value == Guid.Empty)
        {
            return Error.Validation("INVITATION_IDENTITY_REQUIRED", "An invitation and organization identifier are required.");
        }

        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.IsError)
        {
            return normalizedEmail.Errors;
        }

        var normalizedRoles = NormalizeRoles(roles);
        if (normalizedRoles.IsError)
        {
            return normalizedRoles.Errors;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Error.Validation("INVITATION_TOKEN_REQUIRED", "An invitation token is required.");
        }

        if (language is not ("fr" or "en"))
        {
            return Error.Validation("INVITATION_LANGUAGE_INVALID", "Invitation language must be 'fr' or 'en'.");
        }

        return new InvitationAggregate(
            id,
            organizationId,
            normalizedEmail.Value,
            normalizedRoles.Value,
            token,
            language,
            now);
    }

    public InvitationStatus StatusAt(DateTimeOffset now) =>
        Status == InvitationStatus.Pending && ExpiresAt <= now
            ? InvitationStatus.Expired
            : Status;

    public ErrorOr<Success> Accept(string? verifiedEmail, DateTimeOffset now)
    {
        var status = StatusAt(now);
        if (status == InvitationStatus.Expired)
        {
            return Error.Conflict("INVITATION_EXPIRED", "This invitation has expired. Ask an organization administrator to resend it.");
        }

        if (status == InvitationStatus.Revoked)
        {
            return Error.Conflict("INVITATION_REVOKED", "This invitation has been revoked.");
        }

        if (string.IsNullOrWhiteSpace(verifiedEmail))
        {
            return Error.Validation("INVITATION_EMAIL_NOT_VERIFIED", "Sign in with an account whose email address is verified.");
        }

        if (!Email.Equals(verifiedEmail.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation(
                "INVITATION_EMAIL_MISMATCH",
                "This invitation was sent to another address. Ask an administrator to send a new invitation to the verified address.");
        }

        if (status == InvitationStatus.Accepted)
        {
            return Result.Success;
        }

        Status = InvitationStatus.Accepted;
        return Result.Success;
    }

    public ErrorOr<Success> Revoke(DateTimeOffset now)
    {
        if (StatusAt(now) != InvitationStatus.Pending)
        {
            return Error.Conflict("INVITATION_NOT_PENDING", "Only a pending invitation can be revoked.");
        }

        Status = InvitationStatus.Revoked;
        return Result.Success;
    }

    public ErrorOr<Success> Resend(string token, string language, DateTimeOffset now)
    {
        var status = StatusAt(now);
        if (status is InvitationStatus.Accepted or InvitationStatus.Revoked)
        {
            return Error.Conflict("INVITATION_NOT_RESENDABLE", "An accepted or revoked invitation cannot be resent.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Error.Validation("INVITATION_TOKEN_REQUIRED", "An invitation token is required.");
        }

        if (language is not ("fr" or "en"))
        {
            return Error.Validation("INVITATION_LANGUAGE_INVALID", "Invitation language must be 'fr' or 'en'.");
        }

        TokenHash = ComputeTokenHash(token);
        Language = language;
        ExpiresAt = now.AddDays(ValidityDays);
        Status = InvitationStatus.Pending;
        RaiseInvitationCreated(token);
        return Result.Success;
    }

    private void RaiseInvitationCreated(string token) =>
        RaiseDomainEvent(new InvitationCreated(
            Id.Value,
            OrganizationId.Value,
            Email,
            token,
            ExpiresAt,
            Language));

    public static string ComputeTokenHash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static ErrorOr<string> NormalizeEmail(string email)
    {
        var candidate = email?.Trim() ?? string.Empty;
        if (candidate.Length is < 3 or > 320
            || !MailAddress.TryCreate(candidate, out var parsed)
            || !parsed.Address.Equals(candidate, StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation("INVITATION_EMAIL_INVALID", "A valid email address is required.");
        }

        return candidate.ToLowerInvariant();
    }

    private static ErrorOr<OrganizationRole[]> NormalizeRoles(IEnumerable<OrganizationRole> roles)
    {
        var normalizedRoles = roles?.Distinct().ToArray() ?? [];
        if (normalizedRoles.Length == 0)
        {
            return Error.Validation("INVITATION_ROLE_REQUIRED", "At least one organization role is required.");
        }

        if (normalizedRoles.Any(role => !Enum.IsDefined(role)))
        {
            return Error.Validation("INVITATION_ROLE_INVALID", "The organization role is not supported.");
        }

        return normalizedRoles;
    }
}
