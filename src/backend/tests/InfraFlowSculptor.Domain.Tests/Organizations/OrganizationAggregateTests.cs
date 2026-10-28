using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Organization = InfraFlowSculptor.Domain.OrganizationAggregate.OrganizationAggregate;

namespace InfraFlowSculptor.Domain.Tests.Organizations;

public sealed class OrganizationAggregateTests
{
    private static readonly OrganizationRole[] MemberRoles = [OrganizationRole.Member];
    private static readonly OrganizationRole[] ConnectionManagerRoles = [OrganizationRole.ConnectionManager];
    private static readonly string[] DuplicateEmailDomains = [" CONTOSO.EXAMPLE ", "contoso.example"];
    private static readonly Guid[] DuplicateTenantIds =
    [
        Guid.Parse("11111111-1111-4111-8111-111111111111"),
        Guid.Parse("11111111-1111-4111-8111-111111111111")
    ];
    private static readonly string[] ContosoEmailDomains = ["contoso.example"];
    private static readonly Guid[] ContosoTenantIds = [Guid.Parse("11111111-1111-4111-8111-111111111111")];

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("This organization name is longer than eighty characters and must be rejected by validation")]
    public void CreateRejectsNamesOutsideTheAllowedLength(string name)
    {
        var result = Organization.Create(
            new OrganizationId(Guid.NewGuid()),
            name,
            new UserProfileId(Guid.NewGuid()));

        Assert.True(result.IsError);
        Assert.Equal("ORG_NAME_INVALID", result.FirstError.Code);
    }

    [Theory]
    [InlineData("Contoso\r\nBcc: attacker@example.test")]
    [InlineData("Contoso\u007f")]
    public void CreateRejectsControlCharactersInNames(string name)
    {
        var result = Organization.Create(
            new OrganizationId(Guid.NewGuid()),
            name,
            new UserProfileId(Guid.NewGuid()));

        Assert.True(result.IsError);
        Assert.Equal("ORG_NAME_INVALID", result.FirstError.Code);
    }

    [Fact]
    public void RenameRejectsControlCharactersInNames()
    {
        var organization = Organization.Create(
            new OrganizationId(Guid.NewGuid()),
            "Contoso",
            new UserProfileId(Guid.NewGuid())).Value;

        var result = organization.Rename("Updated\r\nBcc: attacker@example.test");

        Assert.True(result.IsError);
        Assert.Equal("ORG_NAME_INVALID", result.FirstError.Code);
        Assert.Equal("Contoso", organization.Name);
    }

    [Fact]
    public void CreateAddsTheCreatorAsAdministrator()
    {
        var result = Organization.Create(
            new OrganizationId(Guid.NewGuid()),
            "  Contoso  ",
            new UserProfileId(Guid.NewGuid()));

        Assert.False(result.IsError);
        Assert.Equal("Contoso", result.Value.Name);
        var creator = Assert.Single(result.Value.Members);
        Assert.Collection(creator.Roles, role => Assert.Equal(OrganizationRole.Administrator, role));
    }

    [Fact]
    public void AddMemberAdvancesOrganizationVersion()
    {
        var organization = Organization.Create(
            new OrganizationId(Guid.NewGuid()),
            "Contoso",
            new UserProfileId(Guid.NewGuid())).Value;

        var result = organization.AddMember(new UserProfileId(Guid.NewGuid()), MemberRoles);

        Assert.False(result.IsError);
        Assert.Equal(2, organization.Version);
    }

    [Fact]
    public void ChangeMemberRolesAdvancesOrganizationVersion()
    {
        var administratorId = new UserProfileId(Guid.NewGuid());
        var memberId = new UserProfileId(Guid.NewGuid());
        var organization = Organization.Create(
            new OrganizationId(Guid.NewGuid()),
            "Contoso",
            administratorId).Value;
        Assert.False(organization.AddMember(memberId, MemberRoles).IsError);
        var previousVersion = organization.Version;

        var result = organization.ChangeMemberRoles(memberId, ConnectionManagerRoles);

        Assert.False(result.IsError);
        Assert.Equal(previousVersion + 1, organization.Version);
    }

    [Fact]
    public void RemoveMemberAdvancesOrganizationVersion()
    {
        var administratorId = new UserProfileId(Guid.NewGuid());
        var memberId = new UserProfileId(Guid.NewGuid());
        var organization = Organization.Create(
            new OrganizationId(Guid.NewGuid()),
            "Contoso",
            administratorId).Value;
        Assert.False(organization.AddMember(memberId, MemberRoles).IsError);
        var previousVersion = organization.Version;

        var result = organization.RemoveMember(memberId);

        Assert.False(result.IsError);
        Assert.Equal(previousVersion + 1, organization.Version);
    }

    [Fact]
    public void ChangeMemberRolesCannotDemoteTheLastAdministrator()
    {
        var administratorId = new UserProfileId(Guid.NewGuid());
        var organization = Organization.Create(
            new OrganizationId(Guid.NewGuid()),
            "Contoso",
            administratorId).Value;

        var result = organization.ChangeMemberRoles(administratorId, ConnectionManagerRoles);

        Assert.True(result.IsError);
        Assert.Equal("RG-ORG-05", result.FirstError.Code);
        Assert.Contains(OrganizationRole.Administrator, Assert.Single(organization.Members).Roles);
    }

    [Fact]
    public void RemoveMemberCannotRemoveTheLastAdministrator()
    {
        var administratorId = new UserProfileId(Guid.NewGuid());
        var organization = Organization.Create(
            new OrganizationId(Guid.NewGuid()),
            "Contoso",
            administratorId).Value;

        var result = organization.RemoveMember(administratorId);

        Assert.True(result.IsError);
        Assert.Equal("RG-ORG-05", result.FirstError.Code);
        Assert.Single(organization.Members);
    }

    [Fact]
    public void RemoveMemberAllowsLeavingWhenAnotherAdministratorRemains()
    {
        var administratorId = new UserProfileId(Guid.NewGuid());
        var memberId = new UserProfileId(Guid.NewGuid());
        var organization = Organization.Create(
            new OrganizationId(Guid.NewGuid()),
            "Contoso",
            administratorId).Value;
        Assert.False(organization.AddMember(memberId, MemberRoles).IsError);

        var result = organization.RemoveMember(memberId);

        Assert.False(result.IsError);
        Assert.Collection(organization.Members, member => Assert.Equal(administratorId, member.UserProfileId));
    }

    [Fact]
    public void UpdateAccessRestrictionsNormalizesAndDeduplicatesValues()
    {
        var organization = Organization.Create(
            new OrganizationId(Guid.NewGuid()),
            "Contoso",
            new UserProfileId(Guid.NewGuid())).Value;

        var result = organization.UpdateAccessRestrictions(DuplicateEmailDomains, DuplicateTenantIds);

        Assert.False(result.IsError);
        Assert.Equal("contoso.example", Assert.Single(organization.AllowedEmailDomains));
        Assert.Equal(Guid.Parse("11111111-1111-4111-8111-111111111111"), Assert.Single(organization.AllowedTenantIds));
    }

    [Fact]
    public void MemberFromAnotherTenantAndDomainIsOutsideAccessRestrictions()
    {
        var organization = Organization.Create(
            new OrganizationId(Guid.NewGuid()),
            "Contoso",
            new UserProfileId(Guid.NewGuid())).Value;
        Assert.False(organization.UpdateAccessRestrictions(ContosoEmailDomains, ContosoTenantIds).IsError);
        var outsideProfile = UserProfile.Create(
            new UserProfileId(Guid.NewGuid()),
            new UserKey(
                Guid.Parse("22222222-2222-4222-8222-222222222222"),
                Guid.NewGuid()),
            "Emma",
            "emma@outlook.example",
            DateTimeOffset.UnixEpoch).Value;

        Assert.True(organization.IsOutsideAccessRestrictions(outsideProfile));
    }

    [Fact]
    public void InvitationEmailAndTenantMustMatchOrganizationRestrictions()
    {
        var organization = Organization.Create(
            new OrganizationId(Guid.NewGuid()),
            "Contoso",
            new UserProfileId(Guid.NewGuid())).Value;
        Assert.False(organization.UpdateAccessRestrictions(ContosoEmailDomains, ContosoTenantIds).IsError);

        Assert.True(organization.IsInvitationEmailAllowed("nina@contoso.example"));
        Assert.False(organization.IsInvitationEmailAllowed("emma@outlook.example"));
        Assert.True(organization.IsInvitationTenantAllowed(ContosoTenantIds[0]));
        Assert.False(organization.IsInvitationTenantAllowed(Guid.Parse("9188040d-6c67-4c5b-b112-36a304b66dad")));
    }
}
