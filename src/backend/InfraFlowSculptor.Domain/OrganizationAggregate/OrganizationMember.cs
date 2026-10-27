using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.OrganizationAggregate;

[AuditedObject(
    ObjectType.OrganizationMember,
    nameof(OrganizationMember.UserProfileId),
    AuditObjectPropertyNames.OrganizationId)]
public sealed class OrganizationMember
{
    private int[] roleValues = [];

    public UserProfileId UserProfileId { get; private set; }

    public int[] RoleValues
    {
        get => roleValues;
        private set => roleValues = value;
    }

    public IReadOnlyCollection<OrganizationRole> Roles =>
        roleValues.Select(value => (OrganizationRole)value).ToArray();

    private OrganizationMember()
    {
    }

    internal OrganizationMember(UserProfileId userProfileId, IEnumerable<OrganizationRole> roles)
    {
        UserProfileId = userProfileId;
        roleValues = roles.Distinct().Select(role => (int)role).ToArray();
    }

    internal void ChangeRoles(IEnumerable<OrganizationRole> roles) =>
        roleValues = roles.Distinct().Select(role => (int)role).ToArray();
}
