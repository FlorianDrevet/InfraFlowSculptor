using InfraFlowSculptor.Application.Common.Plans;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Organizations.Commands.CreateOrganization;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace InfraFlowSculptor.Application.Tests.Organizations;

public sealed class CreateOrganizationCommandHandlerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ifs_application_tests")
        .WithUsername("ifs")
        .WithPassword("ifs_application_test_password")
        .Build();

    public Task InitializeAsync() => database.StartAsync();

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task CreateOrganizationAddsTheAuthenticatedUserAsAdministrator()
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Key.Returns(new UserKey(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")));
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();

        var profile = UserProfile.Create(
            UserProfileId.New(TimeProvider.System),
            currentUser.Key,
            "Alice Martin",
            "alice@contoso.example",
            TimeProvider.System.GetUtcNow()).Value;
        dbContext.UserProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var handler = new CreateOrganizationCommandHandler(
            dbContext,
            currentUser,
            new UnlimitedPlanLimitGuard(),
            TimeProvider.System);

        var result = await handler.Handle(new CreateOrganizationCommand("Contoso Labs"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("Contoso Labs", result.Value.Name);
        Assert.Contains(OrganizationRole.Administrator, result.Value.Roles);
        await dbContext.SaveChangesAsync();
        Assert.Equal(1, await dbContext.Organizations.CountAsync());
    }

    private IfsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IfsDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new IfsDbContext(options, new NoCurrentOrganization());
    }

    private sealed class NoCurrentOrganization : ICurrentOrganization
    {
        public OrganizationId? Id => null;
    }
}
