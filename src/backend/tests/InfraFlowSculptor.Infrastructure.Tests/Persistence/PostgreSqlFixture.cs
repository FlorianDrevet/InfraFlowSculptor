using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ifs_tests")
        .WithUsername("ifs")
        .WithPassword("ifs_test_password")
        .Build();

    public async Task InitializeAsync() => await container.StartAsync();

    public async Task DisposeAsync() => await container.DisposeAsync();

    internal TestIfsDbContext CreateDbContext(ICurrentOrganization currentOrganization)
    {
        var options = new DbContextOptionsBuilder<TestIfsDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TestIfsDbContext(options, currentOrganization);
    }
}
