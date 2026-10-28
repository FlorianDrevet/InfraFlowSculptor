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
    public const string ProjectRoleAssigned = "project.role.assigned";
    public const string ProjectRoleRemoved = "project.role.removed";
    public const string ProjectOwnershipTaken = "project.ownership.taken";
    public const string ComponentCreated = "component.created";
    public const string ComponentUpdated = "component.updated";
    public const string ComponentDeleted = "component.deleted";
    public const string ResourceGroupCreated = "component.resource_group.created";
    public const string ResourceGroupUpdated = "component.resource_group.updated";
    public const string ResourceGroupDeleted = "component.resource_group.deleted";
}
