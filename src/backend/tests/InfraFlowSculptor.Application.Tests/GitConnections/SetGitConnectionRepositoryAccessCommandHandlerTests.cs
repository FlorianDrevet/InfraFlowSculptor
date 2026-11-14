using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Common.Secrets;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace InfraFlowSculptor.Application.Tests.GitConnections;

public sealed class SetGitConnectionRepositoryAccessCommandHandlerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ifs_git_access_tests")
        .WithUsername("ifs")
        .WithPassword("ifs_git_access_test_password")
        .Build();

    public Task InitializeAsync() => database.StartAsync();

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task SetAccessToProjectFromAnotherOrganizationReturnsNotFound()
    {
        var currentOrganizationId = new OrganizationId(Guid.NewGuid());
        var otherOrganizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(currentOrganizationId);
        var creator = UserProfile.Create(
            UserProfileId.New(TimeProvider.System),
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            "Alice Martin",
            "alice@contoso.example",
            TimeProvider.System.GetUtcNow()).Value;
        var currentOrganizationAggregate = OrganizationAggregate.Create(
            currentOrganizationId,
            "Contoso",
            creator.Id).Value;
        var otherOrganizationAggregate = OrganizationAggregate.Create(
            otherOrganizationId,
            "Woodgrove",
            creator.Id).Value;
        var otherProject = ProjectAggregate.Create(
            new ProjectId(Guid.NewGuid()),
            otherOrganizationId,
            "Other shop",
            "othershop",
            creator.Id).Value;
        await using var dbContext = new IfsDbContext(
            new DbContextOptionsBuilder<IfsDbContext>()
                .UseNpgsql(database.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .Options,
            currentOrganization);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.UserProfiles.Add(creator);
        dbContext.Organizations.AddRange(currentOrganizationAggregate, otherOrganizationAggregate);
        dbContext.Projects.Add(otherProject);
        await dbContext.SaveChangesAsync();
        var handler = new SetGitConnectionRepositoryAccessCommandHandler(
            dbContext,
            currentOrganization,
            new GitProviderRegistry([]),
            Substitute.For<ISecretStore>());

        var result = await handler.Handle(
            new SetGitConnectionRepositoryAccessCommand(
                currentOrganizationId.Value,
                Guid.NewGuid(),
                1,
                "contoso/shop",
                false,
                [otherProject.Id.Value]),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("NOT_FOUND", result.FirstError.Code);
    }

    private sealed class TestCurrentOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id => id;
    }
}
