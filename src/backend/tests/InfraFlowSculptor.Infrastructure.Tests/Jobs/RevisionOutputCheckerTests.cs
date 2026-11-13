using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Plan;
using InfraFlowSculptor.Worker.Jobs;

namespace InfraFlowSculptor.Infrastructure.Tests.Jobs;

public sealed class RevisionOutputCheckerTests
{
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
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../../../reference/pilot/bicep-azdo"));
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
