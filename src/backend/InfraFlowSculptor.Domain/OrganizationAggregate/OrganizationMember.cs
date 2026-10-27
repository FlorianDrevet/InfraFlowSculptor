using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Domain.OrganizationAggregate;

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
