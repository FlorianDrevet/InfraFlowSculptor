using System.Security.Cryptography;
using System.Text;
using InfraFlowSculptor.Application;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.RevisionAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Emitters.AzureDevOps;
using InfraFlowSculptor.Emitters.Bicep;
using InfraFlowSculptor.Emitters.InstallKit;
using InfraFlowSculptor.Engine.Plan;
using InfraFlowSculptor.Engine.Validation;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Infrastructure.Tests.Persistence;
using InfraFlowSculptor.Worker.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraFlowSculptor.Infrastructure.Tests.Jobs;

public sealed class GenerateRevisionJobHandlerTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    private const string CatalogVersion = "2026.10-pilot.6";

    [Fact]
    public async Task FrozenInputIsUsedAfterTheProjectModelChanges()
    {
        var catalogProvider = CatalogLoader.Load();
        var testProject = await SeedProjectAsync(catalogProvider);
        await using var dbContext = postgres.CreatePersistenceDbContext(new FixedOrganization(testProject.OrganizationId));
        await dbContext.Database.MigrateAsync();

        using var services = CreateEngineServices(dbContext, catalogProvider);
        using var scope = services.CreateScope();
        var snapshotBuilder = scope.ServiceProvider.GetRequiredService<ModelSnapshotBuilder>();
        var snapshotResult = await snapshotBuilder.BuildWithOrganizationNamingReservationsAsync(
            testProject.ProjectId.Value);
        Assert.False(snapshotResult.IsError);

        var snapshot = snapshotResult.Value;
        Assert.Equal(10, snapshot.Project.ModelVersion);
        AssertHasNoBlockingFindings(snapshot, catalogProvider, scope.ServiceProvider);
        var revision = QueueRevision(dbContext, snapshot, testProject);
        await dbContext.SaveChangesAsync();

        var currentProject = await dbContext.Projects.SingleAsync(project => project.Id == testProject.ProjectId);
        var updatedProject = currentProject.UpdateSettings(
            "Boutique en ligne v11",
            "shop11",
            currentProject.Description,
            currentProject.SystemTagsEnabled,
            currentProject.CoordinatorUserProfileId,
            currentProject.Tags.Select(tag => new KeyValuePair<string, string>(tag.Key, tag.Value)),
            confirmImpact: true);
        Assert.False(updatedProject.IsError);
        currentProject.RecordModelChange();
        await dbContext.SaveChangesAsync();

        var blobStore = new RecordingRevisionBlobStore();
        var handler = CreateHandler(
            scope.ServiceProvider,
            catalogProvider,
            blobStore,
            new AcceptingBicepChecker());

        await handler.HandleAsync(
            new GenerateRevisionJob(revision.Id.Value),
            new JobExecutionContext(Guid.NewGuid(), testProject.OrganizationId, DateTimeOffset.UtcNow),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        Assert.Equal(11, currentProject.ModelVersion);
        Assert.Equal(RevisionState.Generated, revision.State);
        Assert.Equal(10, revision.ModelVersion);
        var plan = DeploymentPlanJsonSerializer.Deserialize(revision.DeploymentPlanJson);
        Assert.Equal("Boutique en ligne", plan.Project.Name);

        var readmeFile = Assert.Single(revision.Files, file => file.RelativePath == "README.ifs.md");
        var readme = Encoding.UTF8.GetString(blobStore.Files[readmeFile.Sha256]);
        Assert.Contains("projet shop", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("shop11", readme, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutputCheckFailureFailsWithoutLoggingClientContent()
    {
        var catalogProvider = CatalogLoader.Load();
        var testProject = await SeedProjectAsync(catalogProvider);
        await using var dbContext = postgres.CreatePersistenceDbContext(new FixedOrganization(testProject.OrganizationId));
        await dbContext.Database.MigrateAsync();

        using var services = CreateEngineServices(dbContext, catalogProvider);
        using var scope = services.CreateScope();
        var snapshotResult = await scope.ServiceProvider.GetRequiredService<ModelSnapshotBuilder>()
            .BuildWithOrganizationNamingReservationsAsync(testProject.ProjectId.Value);
        Assert.False(snapshotResult.IsError);
        AssertHasNoBlockingFindings(snapshotResult.Value, catalogProvider, scope.ServiceProvider);
        var revision = QueueRevision(dbContext, snapshotResult.Value, testProject);
        await dbContext.SaveChangesAsync();

        const string clientContent = "client-resource-secret-marker";
        var blobStore = new RecordingRevisionBlobStore();
        var logger = new CapturingLogger<GenerateRevisionJobHandler>();
        var handler = CreateHandler(scope.ServiceProvider, catalogProvider, blobStore, new FailingBicepChecker(clientContent), logger);

        await handler.HandleAsync(
            new GenerateRevisionJob(revision.Id.Value),
            new JobExecutionContext(Guid.NewGuid(), testProject.OrganizationId, DateTimeOffset.UtcNow),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        Assert.Equal(RevisionState.Failed, revision.State);
        Assert.Equal("OUTPUT_CHECK_FAILED", revision.FailureCode);
        Assert.Empty(revision.Files);
        Assert.Empty(blobStore.Files);
        Assert.Contains(logger.Messages, message => message.Contains("OutputCheckFailed", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains(clientContent, StringComparison.Ordinal));
    }

    private async Task<TestProject> SeedProjectAsync(ICatalogProvider catalogProvider)
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var projectId = new ProjectId(Guid.NewGuid());
        var tenantId = Guid.NewGuid();
        var owner = CreateProfile(tenantId, "Revision Owner");
        var contributor = CreateProfile(tenantId, "Revision Contributor");
        var reader = CreateProfile(tenantId, "Revision Reader");
        var organization = OrganizationAggregate.Create(organizationId, "Revision Test", owner.Id).Value;
        var project = ProjectAggregate.Create(
            projectId,
            organizationId,
            "Boutique en ligne",
            "shop",
            tenantId,
            string.Empty,
            CatalogVersion,
            owner.Id).Value;
        _ = project.AddEnvironment(
            Guid.NewGuid(), "Development", "dev", Guid.NewGuid(), "francecentral", 1,
            isProtected: false, [], [], "Development").Value;
        _ = project.AddEnvironment(
            Guid.NewGuid(), "Production", "prd", Guid.NewGuid(), "francecentral", 2,
            isProtected: true, ["Shop Release Approvers"], [], "Production").Value;

        await using var dbContext = postgres.CreatePersistenceDbContext(new FixedOrganization(organizationId));
        await dbContext.Database.MigrateAsync();
        dbContext.UserProfiles.AddRange(owner, contributor, reader);
        dbContext.Organizations.Add(organization);
        dbContext.Projects.Add(project);
        for (var version = 0; version < 10; version++)
        {
            project.RecordModelChange();
        }

        await dbContext.SaveChangesAsync();
        Assert.NotNull(catalogProvider.GetVersion(CatalogVersion));
        return new TestProject(organizationId, projectId, owner.UserKey);
    }

    private static UserProfile CreateProfile(Guid tenantId, string name) => UserProfile.Create(
        UserProfileId.New(TimeProvider.System),
        new UserKey(tenantId, Guid.NewGuid()),
        name,
        $"{name.Replace(' ', '-').ToLowerInvariant()}@example.test",
        TimeProvider.System.GetUtcNow()).Value;

    private static RevisionAggregate QueueRevision(
        IfsDbContext dbContext,
        InfraFlowSculptor.Engine.Model.ModelSnapshot snapshot,
        TestProject testProject)
    {
        var now = TimeProvider.System.GetUtcNow();
        var snapshotJson = RevisionInputJsonSerializer.Serialize(snapshot);
        var sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson))).ToLowerInvariant();
        var revisionId = RevisionId.New(TimeProvider.System);
        var revision = RevisionAggregate.Queue(
            revisionId,
            testProject.OrganizationId,
            testProject.ProjectId,
            1,
            testProject.Author,
            now,
            snapshot.Project.ModelVersion,
            snapshot.Project.CatalogVersion,
            snapshot.Project.InfrastructureLanguage,
            snapshot.Project.CiPlatform,
            sha256);

        dbContext.Set<RevisionAggregate>().Add(revision);
        dbContext.Set<RevisionInputEntity>().Add(new RevisionInputEntity
        {
            RevisionId = revisionId,
            ModelVersion = snapshot.Project.ModelVersion,
            CatalogVersion = snapshot.Project.CatalogVersion,
            InfrastructureLanguage = snapshot.Project.InfrastructureLanguage,
            CiPlatform = snapshot.Project.CiPlatform,
            Snapshot = snapshotJson,
            Sha256 = sha256
        });

        return revision;
    }

    private static void AssertHasNoBlockingFindings(
        InfraFlowSculptor.Engine.Model.ModelSnapshot snapshot,
        ICatalogProvider catalogProvider,
        IServiceProvider services)
    {
        var catalog = catalogProvider.GetVersion(snapshot.Project.CatalogVersion);
        Assert.NotNull(catalog);
        var blockingFindings = services.GetRequiredService<EngineFacade>()
            .Evaluate(snapshot, catalog!)
            .Validation.Findings
            .Where(finding => finding.Severity == FindingSeverity.Error)
            .Select(finding => $"{finding.Code}: {finding.MessageEn}")
            .ToArray();
        Assert.True(blockingFindings.Length == 0, $"The test project has blocking findings: {string.Join("; ", blockingFindings)}");
    }

    private static ServiceProvider CreateEngineServices(IfsDbContext dbContext, ICatalogProvider catalogProvider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton<ICatalogProvider>(catalogProvider);
        services.AddSingleton<IIfsDbContext>(dbContext);
        services.AddBicepEmitter();
        services.AddAzureDevOpsEmitters();
        services.AddInstallKitEmitter();
        return services.BuildServiceProvider();
    }

    private static GenerateRevisionJobHandler CreateHandler(
        IServiceProvider services,
        ICatalogProvider catalogProvider,
        IRevisionBlobStore blobStore,
        IOutputChecker outputChecker,
        ILogger<GenerateRevisionJobHandler>? logger = null) => new(
            services.GetRequiredService<IIfsDbContext>(),
            catalogProvider,
            services.GetRequiredService<EngineFacade>(),
            services.GetRequiredService<InfrastructureEmitterRegistry>(),
            services.GetRequiredService<PipelineEmitterRegistry>(),
            services.GetRequiredService<InstallKitPlatformEmitterRegistry>(),
            new RevisionOutputChecker(new OutputCheckerRegistry([outputChecker])),
            blobStore,
            TimeProvider.System,
            logger ?? NullLogger<GenerateRevisionJobHandler>.Instance);

    private sealed record TestProject(
        OrganizationId OrganizationId,
        ProjectId ProjectId,
        UserKey Author);

    private sealed class FixedOrganization(OrganizationId id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }

    private sealed class RecordingRevisionBlobStore : IRevisionBlobStore
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

        public Task EnsureFileAsync(string sha256, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
        {
            Files.TryAdd(sha256, content.ToArray());
            return Task.CompletedTask;
        }

        public Task<Stream> OpenFileAsync(string sha256, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Stream> GetOrCreateArchiveAsync(
            Guid revisionId,
            IReadOnlyCollection<RevisionFile> files,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<long> DeleteUnreferencedFilesAsync(
            IReadOnlySet<string> referencedSha256,
            DateTimeOffset olderThan,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<long> DeleteUnretainedArchivesAsync(
            IReadOnlySet<Guid> retainedRevisionIds,
            DateTimeOffset olderThan,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class AcceptingBicepChecker : IOutputChecker
    {
        public IacLanguage Key => IacLanguage.Bicep;

        public ValueTask CheckAsync(
            IReadOnlyList<GeneratedFile> files,
            CatalogPins pins,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class FailingBicepChecker(string clientContent) : IOutputChecker
    {
        public IacLanguage Key => IacLanguage.Bicep;

        public ValueTask CheckAsync(
            IReadOnlyList<GeneratedFile> files,
            CatalogPins pins,
            CancellationToken cancellationToken) =>
            ValueTask.FromException(new InvalidOperationException(clientContent));
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
