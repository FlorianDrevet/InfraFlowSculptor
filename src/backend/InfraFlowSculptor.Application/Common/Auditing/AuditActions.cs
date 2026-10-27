namespace InfraFlowSculptor.Application.Common.Auditing;

public static class AuditActions
{
    public const string OrganizationCreated = "organization.created";
    public const string OrganizationRenamed = "organization.renamed";
    public const string OrganizationMemberRolesChanged = "organization.member.roles_changed";
    public const string OrganizationMemberRemoved = "organization.member.removed";
    public const string OrganizationMemberLeft = "organization.member.left";
    public const string OrganizationAccessRestrictionsUpdated = "organization.access_restrictions.updated";
    public const string InvitationSent = "organization.invitation.sent";
    public const string InvitationResent = "organization.invitation.resent";
    public const string InvitationRevoked = "organization.invitation.revoked";
    public const string InvitationAccepted = "organization.invitation.accepted";
}
