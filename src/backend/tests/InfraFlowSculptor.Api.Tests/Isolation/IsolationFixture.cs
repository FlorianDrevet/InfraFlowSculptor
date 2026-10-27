using InfraFlowSculptor.Api.Configuration;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Api.Tests.Isolation;

public static class IsolationFixture
{
    public static readonly Guid ContosoId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    public static readonly Guid FabrikamId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    public static async Task SeedAsync(ApiFactory factory)
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        await DemoDataSeeder.SeedAsync(dbContext, TimeProvider.System);
    }
}
