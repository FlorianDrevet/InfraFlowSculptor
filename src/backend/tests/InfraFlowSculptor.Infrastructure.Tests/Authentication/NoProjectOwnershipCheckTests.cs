using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Infrastructure.Authentication;

namespace InfraFlowSculptor.Infrastructure.Tests.Authentication;

public sealed class NoProjectOwnershipCheckTests
{
    [Fact]
    public async Task ReportsNoOwnedProjectsUntilProjectOwnershipExists()
    {
        var sut = new NoProjectOwnershipCheck();

        var result = await sut.WouldRemovingMemberRemoveLastProjectOwnerAsync(
            new OrganizationId(Guid.NewGuid()),
            new UserProfileId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result);
    }
}
