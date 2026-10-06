using System.Collections.Generic;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Infrastructure.Azure;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class DbContextExecutionStrategyTests
{
    [Fact]
    public void AddIfsDbContextDisablesAutomaticRetryToSupportExplicitTransactions()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ifs"] = "Host=localhost;Database=ifs;Username=ifs;Password=Ifs-Test-Only-2026!"
        });
        builder.Services.AddSingleton<IfsAzureCredential>();
        builder.Services.AddScoped<ICurrentOrganization, TestCurrentOrganization>();
        builder.AddIfsDbContext("ifs");

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();

        Assert.False(dbContext.Database.CreateExecutionStrategy().RetriesOnFailure);
    }

    private sealed class TestCurrentOrganization : ICurrentOrganization
    {
        public OrganizationId? Id => null;
    }
}
