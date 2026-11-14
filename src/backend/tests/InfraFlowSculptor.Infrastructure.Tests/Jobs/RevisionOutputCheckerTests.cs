using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Plan;
using InfraFlowSculptor.Worker.Jobs;

namespace InfraFlowSculptor.Infrastructure.Tests.Jobs;

public sealed class RevisionOutputCheckerTests
{
    [Fact]
    public async Task CheckAsyncPreservesValidationFailureWhenTemporaryDirectoryCleanupFails()
    {
        var cleanupFailure = new IOException("temporary directory cleanup failed");
        var checker = new RevisionOutputChecker(
            new OutputCheckerRegistry([new AcceptingBicepChecker()]),
            root =>
            {
                Directory.Delete(root, recursive: true);
                return Task.FromException(cleanupFailure);
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => checker.CheckAsync(
            [new GeneratedFile("../outside.ps1", "Write-Output 'ready'\n")],
            IacLanguage.Bicep,
            GetPins(),
            CancellationToken.None));

        Assert.Equal("A generated output path escapes its validation directory.", exception.Message);
        Assert.Same(cleanupFailure, exception.Data[RevisionOutputChecker.TemporaryDirectoryCleanupFailureDataKey]);
    }

    [Fact]
    public async Task CheckAsyncPropagatesTemporaryDirectoryCleanupFailureAfterSuccessfulValidation()
    {
        var cleanupFailure = new IOException("temporary directory cleanup failed");
        var checker = new RevisionOutputChecker(
            new OutputCheckerRegistry([new AcceptingBicepChecker()]),
            root =>
            {
                Directory.Delete(root, recursive: true);
                return Task.FromException(cleanupFailure);
            });

        var exception = await Assert.ThrowsAsync<IOException>(() => checker.CheckAsync(
            [new GeneratedFile("scripts/deploy.ps1", "Write-Output 'ready'\n")],
            IacLanguage.Bicep,
            GetPins(),
            CancellationToken.None));

        Assert.Same(cleanupFailure, exception);
    }

    [Fact]
    public async Task CheckAsyncRejectsDuplicateGeneratedOutputPaths()
    {
        var checker = CreateChecker();
        GeneratedFile[] files =
        [
            new("scripts/Deploy.ps1", "Write-Output 'first'\n"),
            new("scripts/deploy.ps1", "Write-Output 'second'\n")
        ];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            checker.CheckAsync(files, IacLanguage.Bicep, GetPins(), CancellationToken.None));

        Assert.Contains("is duplicated", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsyncRejectsBackslashOutputPathSeparators()
    {
        var checker = CreateChecker();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => checker.CheckAsync(
            [new GeneratedFile("scripts\\deploy.ps1", "Write-Output 'ready'\n")],
            IacLanguage.Bicep,
            GetPins(),
            CancellationToken.None));

        Assert.Contains("forward slash", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsyncRejectsRootedOutputPaths()
    {
        var rootedPath = Path.Combine(Path.GetTempPath(), "outside.ps1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateChecker().CheckAsync(
            [new GeneratedFile(rootedPath, "Write-Output 'ready'\n")],
            IacLanguage.Bicep,
            GetPins(),
            CancellationToken.None));

        Assert.Contains("must be relative", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsyncValidatesPipelineSchemaAndPowerShellScripts()
    {
        var checker = CreateChecker();
        GeneratedFile[] files =
        [
            new("azure-pipelines.yml", "trigger: none\nstages:\n  - stage: Build\n    jobs:\n      - job: Validate\n        steps:\n          - script: echo ready\n"),
            new("scripts/deploy.ps1", "Write-Output 'ready'\n")
        ];

        await checker.CheckAsync(files, IacLanguage.Bicep, GetPins(), CancellationToken.None);
    }

    [Fact]
    public async Task CheckAsyncValidatesPowerShellOnlyOutput()
    {
        await CreateChecker().CheckAsync(
            [new GeneratedFile("scripts/deploy.ps1", "Write-Output 'ready'\n")],
            IacLanguage.Bicep,
            GetPins(),
            CancellationToken.None);
    }

    [Fact]
    public async Task CheckAsyncValidatesYamlOnlyOutput()
    {
        await CreateChecker().CheckAsync(
            [new GeneratedFile(
                "azure-pipelines.yml",
                "trigger: none\nstages:\n  - stage: Build\n    jobs:\n      - job: Validate\n        steps:\n          - script: echo ready\n")],
            IacLanguage.Bicep,
            GetPins(),
            CancellationToken.None);
    }

    [Fact]
    public async Task CheckAsyncRejectsPowerShellParserErrorsInHiddenDirectories()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateChecker().CheckAsync(
            [new GeneratedFile(".ifs/templates/scripts/deploy.ps1", "function Broken-Output {\n")],
            IacLanguage.Bicep,
            GetPins(),
            CancellationToken.None));
    }

    [Fact]
    public async Task CheckAsyncRejectsPipelineSchemaErrorsInHiddenDirectories()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateChecker().CheckAsync(
            [new GeneratedFile(".ifs/pipelines/azure-pipelines.yml", "unsupportedPipelineProperty: true\n")],
            IacLanguage.Bicep,
            GetPins(),
            CancellationToken.None));
    }

    [Fact]
    public async Task CheckAsyncParsesYamlInHiddenTemplatesButSkipsItsPipelineSchema()
    {
        await CreateChecker().CheckAsync(
            [new GeneratedFile(".ifs/templates/pipeline.yml", "templateOnlyProperty: true\n")],
            IacLanguage.Bicep,
            GetPins(),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateChecker().CheckAsync(
            [new GeneratedFile(".ifs/templates/broken.yml", "invalid: [\n")],
            IacLanguage.Bicep,
            GetPins(),
            CancellationToken.None));
    }

    [Fact]
    public async Task CheckAsyncRejectsPowerShellParserErrors()
    {
        var checker = CreateChecker();
        GeneratedFile[] files =
        [
            new("azure-pipelines.yml", "trigger: none\nstages:\n  - stage: Build\n    jobs:\n      - job: Validate\n        steps:\n          - script: echo ready\n"),
            new("scripts/deploy.ps1", "function Broken-Output {\n  $value = (\n}\n")
        ];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            checker.CheckAsync(files, IacLanguage.Bicep, GetPins(), CancellationToken.None));
    }

    [Fact]
    public async Task CheckAsyncValidatesGeneratedRootFileNamedLikeTheCheckerScript()
    {
        var checker = CreateChecker();
        GeneratedFile[] files =
        [
            new("check-output.ps1", "function Broken-Output {\n")
        ];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            checker.CheckAsync(files, IacLanguage.Bicep, GetPins(), CancellationToken.None));
    }

    [Fact]
    public async Task CheckAsyncRejectsPipelineThatDoesNotMatchPinnedSchema()
    {
        var checker = CreateChecker();
        GeneratedFile[] files =
        [
            new("azure-pipelines.yml", "unsupportedPipelineProperty: true\n")
        ];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            checker.CheckAsync(files, IacLanguage.Bicep, GetPins(), CancellationToken.None));
    }

    [Fact]
    public async Task CheckAsyncAcceptsThePinnedPilotReferencePipelineAndScripts()
    {
        var root = Path.Combine(FindRepositoryRoot(), "reference", "pilot", "bicep-azdo");
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".yml" or ".yaml" or ".ps1" or ".psm1")
            .Select(path => new GeneratedFile(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                File.ReadAllText(path)))
            .ToArray();

        await CreateChecker().CheckAsync(files, IacLanguage.Bicep, GetPins(), CancellationToken.None);
    }

    private static RevisionOutputChecker CreateChecker() =>
        new(new OutputCheckerRegistry([new AcceptingBicepChecker()]));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "reference", "pilot", "bicep-azdo")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root could not be found from the test output directory.");
    }

    private static CatalogPins GetPins()
    {
        var catalog = CatalogLoader.Load().GetVersion("2026.10-pilot.6");
        Assert.NotNull(catalog);
        return new CatalogPins(
            catalog!.Version,
            catalog.Fingerprint,
            catalog.Pins.BicepCliVersion,
            catalog.Pins.StartupImage,
            catalog.Pins.Modules,
            catalog.Pins.PowerShellModules,
            catalog.Pins.PipelineValidation,
            catalog.Pins.NativeResources);
    }

    private sealed class AcceptingBicepChecker : IOutputChecker
    {
        public IacLanguage Key => IacLanguage.Bicep;

        public ValueTask CheckAsync(
            IReadOnlyList<GeneratedFile> files,
            CatalogPins pins,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
