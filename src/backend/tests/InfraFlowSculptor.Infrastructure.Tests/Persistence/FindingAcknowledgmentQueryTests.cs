using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class FindingAcknowledgmentQueryTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("Hosting", false)]
    public void NullableDetailKeyPredicateTranslatesToPostgreSql(string? detailKey, bool expectsNullCheck)
    {
        var currentOrganization = Substitute.For<ICurrentOrganization>();
        currentOrganization.Id.Returns(new OrganizationId(Guid.NewGuid()));
        var options = new DbContextOptionsBuilder<IfsDbContext>()
            .UseNpgsql("Host=localhost;Database=ifs;Username=ifs;Password=ifs")
            .Options;
        using var dbContext = new IfsDbContext(options, currentOrganization);
        var projectId = new ProjectId(Guid.NewGuid());

        var sql = dbContext.Set<FindingAcknowledgmentAggregate>()
            .Where(candidate => candidate.ProjectId == projectId
                && candidate.DetailKey == detailKey)
            .ToQueryString();

        Assert.Contains("DetailKey", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectsNullCheck, sql.Contains("IS NULL", StringComparison.OrdinalIgnoreCase));
    }
}
