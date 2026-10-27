using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Organization = InfraFlowSculptor.Domain.OrganizationAggregate.OrganizationAggregate;

namespace InfraFlowSculptor.Domain.Tests.Organizations;

public sealed class OrganizationAggregateTests
{
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
}
