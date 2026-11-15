using InfraFlowSculptor.Application.Common.Notifications;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Worker.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Testcontainers.PostgreSql;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Infrastructure.Tests.GitConnections;

public sealed class GitConnectionExpiryAlertScheduledJobTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ifs_expiry_alert_tests")
        .WithUsername("ifs")
        .WithPassword("ifs_expiry_alert_test_password")
        .Build();

    public Task InitializeAsync() => database.StartAsync();

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task ScheduledJobQueriesFallbackConnectionsAndSendsOnlyTheMostUrgentDueAlert()
    {
        var now = new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);
        var organizationId = new OrganizationId(Guid.NewGuid());
        var workerOrganization = new WorkerOrganizationContext();
        workerOrganization.Set(organizationId);
        await using var dbContext = new IfsDbContext(
            new DbContextOptionsBuilder<IfsDbContext>()
                .UseNpgsql(database.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .Options,
            workerOrganization);
        await dbContext.Database.EnsureCreatedAsync();

        var profile = UserProfile.Create(
            UserProfileId.New(clock),
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            "Alice Martin",
            "alice@contoso.example",
            now).Value;
        var organization = OrganizationAggregate.Create(organizationId, "Contoso", profile.Id).Value;
        var connection = GitConnection.Create(
            new GitConnectionId(Guid.NewGuid()),
            organizationId,
            GitProviderKind.AzureRepos,
            "Azure Repos",
            "shop42",
            Guid.NewGuid(),
            GitConnectionAuthenticationMode.PersonalAccessToken,
            now.AddDays(2),
            "git-conn-test",
            now).Value;
        dbContext.UserProfiles.Add(profile);
        dbContext.Organizations.Add(organization);
        dbContext.GitConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        var emailSender = Substitute.For<IEmailSender>();
        var processor = new GitConnectionExpiryAlertProcessor(
            dbContext,
            workerOrganization,
            emailSender,
            new ConfigurationBuilder().Build(),
            clock,
            Substitute.For<Microsoft.Extensions.Logging.ILogger<GitConnectionExpiryAlertProcessor>>());
        var job = new GitConnectionExpiryAlertScheduledJob(
            dbContext,
            processor,
            Substitute.For<Microsoft.Extensions.Logging.ILogger<GitConnectionExpiryAlertScheduledJob>>());

        await job.RunAsync(CancellationToken.None);

        await emailSender.Received(1).SendAsync(
            Arg.Is<OutgoingEmail>(email => email.To == "alice@contoso.example"),
            Arg.Any<CancellationToken>());
        var savedConnection = await dbContext.GitConnections.SingleAsync(candidate => candidate.Id == connection.Id);
        Assert.True(savedConnection.Expiry15DayAlertSent);
        Assert.True(savedConnection.Expiry3DayAlertSent);
    }

    [Fact]
    public async Task ScheduledJobContinuesWithOtherOrganizationsAfterAnEmailFailure()
    {
        var now = new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);
        var firstOrganizationId = new OrganizationId(Guid.NewGuid());
        var secondOrganizationId = new OrganizationId(Guid.NewGuid());
        var workerOrganization = new WorkerOrganizationContext();
        await using var dbContext = new IfsDbContext(
            new DbContextOptionsBuilder<IfsDbContext>()
                .UseNpgsql(database.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .Options,
            workerOrganization);
        await dbContext.Database.EnsureCreatedAsync();

        var firstProfile = CreateProfile("Alice Martin", "alice@contoso.example", now);
        var secondAdministratorProfile = CreateProfile("Chloé Martin", "chloe@contoso.example", now);
        var secondProfile = CreateProfile("Bob Martin", "bob@woodgrove.example", now);
        var firstOrganization = OrganizationAggregate.Create(firstOrganizationId, "Contoso", firstProfile.Id).Value;
        Assert.False(firstOrganization.AddMember(
            secondAdministratorProfile.Id,
            [OrganizationRole.Administrator]).IsError);
        var secondOrganization = OrganizationAggregate.Create(secondOrganizationId, "Woodgrove", secondProfile.Id).Value;
        dbContext.UserProfiles.AddRange(firstProfile, secondAdministratorProfile, secondProfile);
        dbContext.Organizations.AddRange(firstOrganization, secondOrganization);
        dbContext.GitConnections.AddRange(
            CreateExpiringConnection(firstOrganizationId, "Contoso", now),
            CreateExpiringConnection(secondOrganizationId, "Woodgrove", now));
        await dbContext.SaveChangesAsync();

        var emailSender = Substitute.For<IEmailSender>();
        emailSender.SendAsync(
                Arg.Is<OutgoingEmail>(email => email.To == "alice@contoso.example"),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("simulated mail failure")));
        var processor = new GitConnectionExpiryAlertProcessor(
            dbContext,
            workerOrganization,
            emailSender,
            new ConfigurationBuilder().Build(),
            clock,
            Substitute.For<Microsoft.Extensions.Logging.ILogger<GitConnectionExpiryAlertProcessor>>());
        var job = new GitConnectionExpiryAlertScheduledJob(
            dbContext,
            processor,
            Substitute.For<Microsoft.Extensions.Logging.ILogger<GitConnectionExpiryAlertScheduledJob>>());

        await job.RunAsync(CancellationToken.None);

        await emailSender.Received(1).SendAsync(
            Arg.Is<OutgoingEmail>(email => email.To == "chloe@contoso.example"),
            Arg.Any<CancellationToken>());
        await emailSender.Received(1).SendAsync(
            Arg.Is<OutgoingEmail>(email => email.To == "bob@woodgrove.example"),
            Arg.Any<CancellationToken>());
    }

    private static UserProfile CreateProfile(string name, string email, DateTimeOffset now) =>
        UserProfile.Create(
            UserProfileId.New(TimeProvider.System),
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            name,
            email,
            now).Value;

    private static GitConnection CreateExpiringConnection(
        OrganizationId organizationId,
        string organizationName,
        DateTimeOffset now) =>
        GitConnection.Create(
            new GitConnectionId(Guid.NewGuid()),
            organizationId,
            GitProviderKind.AzureRepos,
            "Azure Repos",
            organizationName,
            Guid.NewGuid(),
            GitConnectionAuthenticationMode.PersonalAccessToken,
            now.AddDays(2),
            $"git-conn-{organizationId.Value:N}",
            now).Value;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
