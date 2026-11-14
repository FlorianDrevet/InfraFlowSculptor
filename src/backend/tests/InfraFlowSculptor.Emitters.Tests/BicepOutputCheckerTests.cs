using System.IO;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Emitters.Bicep;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.Tests;

public sealed class BicepOutputCheckerTests
{
    [BicepCliFact]
    public async Task CheckAsyncValidatesGeneratedPilotWithThePinnedCli()
    {
        await CheckGeneratedPilotAsync(new BicepCommandRunner());
    }

    [AzureCliBicepFact]
    public async Task CheckAsyncValidatesGeneratedPilotThroughTheAzureCliFallback()
    {
        var azureCliInvocation = BicepCommandRunner.GetAzureCliInvocation();
        Assert.NotNull(azureCliInvocation);
        var runner = new BicepCommandRunner((startInfo, cancellationToken) =>
            startInfo.FileName == azureCliInvocation.Value.Executable
                ? BicepCommandRunner.ExecuteProcessAsync(startInfo, cancellationToken)
                : Task.FromException<BicepCommandResult>(new System.ComponentModel.Win32Exception(2, "not found")),
            () => azureCliInvocation);

        await CheckGeneratedPilotAsync(runner);
    }

    private static async Task CheckGeneratedPilotAsync(IBicepCommandRunner runner)
    {
        var catalog = CatalogLoader.Load().GetVersion("2026.10-pilot.6");
        Assert.NotNull(catalog);
        var plan = PilotReferenceModel.Create(catalog!);
        Assert.Single(plan.Components, component => component.ComponentCode == "orders");
        plan = plan with
        {
            Components = plan.Components.Select(component => component.ComponentCode == "orders"
                ? component with
                {
                    Targets = component.Targets.Select(target => target with
                    {
                        ResourceGroups = target.ResourceGroups.Append(new ResourceGroupPlan(
                            "observability-core",
                            $"rg-shop-observability-core-{target.Target.Code}",
                            "francecentral",
                            true,
                            new Dictionary<string, string>(StringComparer.Ordinal))).ToArray()
                    }).ToArray()
                }
                : component).ToArray()
        };
        var emitter = new BicepInfrastructureEmitter(
            new ModuleSourceStrategyRegistry([new PublicAvmModuleSourceStrategy()]));
        var files = emitter.Emit(plan, catalog!);
        var checker = new BicepOutputChecker(runner);

        await checker.CheckAsync(files, plan.Pins, CancellationToken.None);
    }

    [Fact]
    public async Task CheckAsyncBuildsLintsFormatsAndChecksParametersWithThePinnedCli()
    {
        var runner = new RecordingBicepCommandRunner();
        var checker = new BicepOutputChecker(runner);
        GeneratedFile[] files =
        [
            new("core/infra/main.bicep", "targetScope = 'subscription'\n"),
            new("core/infra/main.dev.bicepparam", "using 'main.bicep'\n"),
            new("core/infra/role-assignment.bicep", "targetScope = 'resourceGroup'\n")
        ];
        var pins = GetPins();

        await checker.CheckAsync(files, pins, CancellationToken.None);

        Assert.Contains(runner.Commands, command => command.SequenceEqual(["--version"]));
        Assert.Contains(runner.Commands, command => command.SequenceEqual(
            ["build", "core/infra/main.bicep", "--stdout"]));
        Assert.Contains(runner.Commands, command => command.SequenceEqual(
            ["lint", "core/infra/main.bicep"]));
        Assert.Contains(runner.Commands, command => command.SequenceEqual(
            ["build-params", "core/infra/main.dev.bicepparam", "--stdout"]));
        Assert.Equal(2, runner.Commands.Count(command => command[0] == "format"));
    }

    [Fact]
    public async Task CheckAsyncRejectsGeneratedPathsThatEscapeTheTemporaryDirectory()
    {
        var checker = new BicepOutputChecker(new RecordingBicepCommandRunner());
        GeneratedFile[] files = [new("../outside.bicep", "targetScope = 'subscription'\n")];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await checker.CheckAsync(files, GetPins(), CancellationToken.None));

        Assert.Contains("escapes its validation directory", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsyncRejectsDuplicateGeneratedPathsIgnoringCase()
    {
        var checker = new BicepOutputChecker(new RecordingBicepCommandRunner());
        GeneratedFile[] files =
        [
            new("core/infra/main.bicep", "targetScope = 'subscription'\n"),
            new("core/infra/MAIN.BICEP", "targetScope = 'resourceGroup'\n"),
            new("core/infra/main.dev.bicepparam", "using 'main.bicep'\n")
        ];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await checker.CheckAsync(files, GetPins(), CancellationToken.None));

        Assert.Contains("is duplicated", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsyncRejectsGeneratedFilesInTheReservedFormatDirectory()
    {
        var checker = new BicepOutputChecker(new RecordingBicepCommandRunner());
        GeneratedFile[] files =
        [
            new(".format-check/0001-role-assignment.bicep", "targetScope = 'resourceGroup'\n"),
            new("core/infra/main.bicep", "targetScope = 'subscription'\n"),
            new("core/infra/main.dev.bicepparam", "using 'main.bicep'\n"),
            new("core/infra/role-assignment.bicep", "targetScope = 'resourceGroup'\n")
        ];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await checker.CheckAsync(files, GetPins(), CancellationToken.None));

        Assert.Contains("reserved", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsyncRejectsAbsoluteGeneratedPaths()
    {
        var checker = new BicepOutputChecker(new RecordingBicepCommandRunner());
        GeneratedFile[] files =
        [
            new(Path.Combine(Path.GetTempPath(), "outside.bicep"), "targetScope = 'subscription'\n")
        ];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await checker.CheckAsync(files, GetPins(), CancellationToken.None));

        Assert.Contains("must be relative", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsyncRejectsGeneratedPathsThatDoNotIdentifyAFile()
    {
        var checker = new BicepOutputChecker(new RecordingBicepCommandRunner());
        GeneratedFile[] files = [new(".", "not a file")];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await checker.CheckAsync(files, GetPins(), CancellationToken.None));

        Assert.Contains("must identify a file", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsyncRejectsGeneratedPathsWithBackslashSeparators()
    {
        var checker = new BicepOutputChecker(new RecordingBicepCommandRunner());
        GeneratedFile[] files = [new("core\\infra\\main.bicep", "targetScope = 'subscription'\n")];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await checker.CheckAsync(files, GetPins(), CancellationToken.None));

        Assert.Contains("forward slash", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsyncRejectsIncompleteGeneratedFileSets()
    {
        var checker = new BicepOutputChecker(new RecordingBicepCommandRunner());
        GeneratedFile[] files =
        [
            new("core/infra/role-assignment.bicep", "targetScope = 'resourceGroup'\n"),
            new("core/infra/main.dev.bicepparam", "using 'main.bicep'\n")
        ];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await checker.CheckAsync(files, GetPins(), CancellationToken.None));

        Assert.Contains("at least one main.bicep", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsyncRejectsNonzeroBuildExitCode()
    {
        var runner = new RecordingBicepCommandRunner { FailedCommand = "build" };
        var checker = new BicepOutputChecker(runner);
        GeneratedFile[] files =
        [
            new("core/infra/main.bicep", "targetScope = 'subscription'\n"),
            new("core/infra/main.dev.bicepparam", "using 'main.bicep'\n")
        ];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await checker.CheckAsync(files, GetPins(), CancellationToken.None));

        Assert.Contains("exit code 1", exception.Message, StringComparison.Ordinal);
        Assert.Equal(["--version", "build"], runner.Commands.Select(command => command[0]));
    }

    [Fact]
    public async Task CheckAsyncRejectsFilesThatWouldChangeWhenFormatted()
    {
        var runner = new RecordingBicepCommandRunner { FormatOutputDiffers = true };
        var checker = new BicepOutputChecker(runner);
        GeneratedFile[] files =
        [
            new("core/infra/main.bicep", "targetScope = 'subscription'\n"),
            new("core/infra/main.dev.bicepparam", "using 'main.bicep'\n")
        ];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await checker.CheckAsync(files, GetPins(), CancellationToken.None));

        Assert.Contains("format would change", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsyncPreservesValidationFailureWhenTemporaryCleanupAlsoFails()
    {
        string? temporaryDirectory = null;
        var runner = new RecordingBicepCommandRunner { LintWarning = true };
        var checker = new BicepOutputChecker(runner, path =>
        {
            temporaryDirectory = path;
            return Task.FromException(new IOException("Simulated cleanup failure."));
        });
        GeneratedFile[] files =
        [
            new("core/infra/main.bicep", "targetScope = 'subscription'\n"),
            new("core/infra/main.dev.bicepparam", "using 'main.bicep'\n")
        ];

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await checker.CheckAsync(files, GetPins(), CancellationToken.None));

            Assert.Contains("produced a warning", exception.Message, StringComparison.Ordinal);
            Assert.IsType<IOException>(exception.Data[BicepOutputChecker.TemporaryDirectoryCleanupFailureDataKey]);
        }
        finally
        {
            if (temporaryDirectory is not null && Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DeleteTemporaryDirectoryRetriesATransientIoFailure()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ifs-bicep-check-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var attempts = 0;
        try
        {
            await BicepOutputChecker.DeleteTemporaryDirectoryAsync(root, (path, recursive) =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new IOException("Simulated transient directory lock.");
                }

                Directory.Delete(path, recursive);
            });

            Assert.Equal(2, attempts);
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DeleteTemporaryDirectoryTreatsDirectoryAlreadyRemovedAsSuccess()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ifs-bicep-check-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var attempts = 0;
        await BicepOutputChecker.DeleteTemporaryDirectoryAsync(root, (path, recursive) =>
        {
            attempts++;
            Directory.Delete(path, recursive);
            throw new DirectoryNotFoundException("Simulated removal race.");
        });

        Assert.Equal(1, attempts);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task CheckAsyncRejectsWarningsFromBicepLint()
    {
        var runner = new RecordingBicepCommandRunner { LintWarning = true };
        var checker = new BicepOutputChecker(runner);
        GeneratedFile[] files =
        [
            new("core/infra/main.bicep", "targetScope = 'subscription'\n"),
            new("core/infra/main.dev.bicepparam", "using 'main.bicep'\n")
        ];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await checker.CheckAsync(files, GetPins(), CancellationToken.None));

        Assert.Contains("warning", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsyncDoesNotTreatJsonDescriptionAsBicepWarning()
    {
        var runner = new RecordingBicepCommandRunner { CompiledOutputContainsWarning = true };
        var checker = new BicepOutputChecker(runner);
        GeneratedFile[] files =
        [
            new("core/infra/main.bicep", "targetScope = 'subscription'\n"),
            new("core/infra/main.dev.bicepparam", "using 'main.bicep'\n")
        ];

        await checker.CheckAsync(files, GetPins(), CancellationToken.None);
    }

    [Theory]
    [InlineData("Bicep CLI version 0.47.160")]
    [InlineData("Bicep CLI version 0.47.16-preview.1")]
    [InlineData("Bicep CLI version 0.47.16.1")]
    public async Task CheckAsyncRejectsVersionThatOnlySharesThePinnedVersionPrefix(string versionOutput)
    {
        var runner = new RecordingBicepCommandRunner { VersionOutput = versionOutput };
        var checker = new BicepOutputChecker(runner);
        GeneratedFile[] files =
        [
            new("core/infra/main.bicep", "targetScope = 'subscription'\n"),
            new("core/infra/main.dev.bicepparam", "using 'main.bicep'\n")
        ];

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await checker.CheckAsync(files, GetPins(), CancellationToken.None));
    }

    private static CatalogPins GetPins()
    {
        var catalog = CatalogLoader.Load().GetVersion("2026.10-pilot.6");
        Assert.NotNull(catalog);
        return PilotReferenceModel.Create(catalog!).Pins;
    }

    private sealed class RecordingBicepCommandRunner : IBicepCommandRunner
    {
        public List<string[]> Commands { get; } = [];

        public bool LintWarning { get; init; }

        public bool CompiledOutputContainsWarning { get; init; }

        public bool FormatOutputDiffers { get; init; }

        public string? FailedCommand { get; init; }

        public string VersionOutput { get; init; } = "Bicep CLI version 0.47.16";

        public Task<BicepCommandResult> RunAsync(
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var command = arguments.ToArray();
            Commands.Add(command);

            if (command.SequenceEqual(["--version"]))
            {
                return Task.FromResult(new BicepCommandResult(0, VersionOutput, string.Empty));
            }

            if (command[0] == "format")
            {
                var file = Path.Combine(workingDirectory, command[1]);
                var output = Path.Combine(workingDirectory, command[Array.IndexOf(command, "--outfile") + 1]);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                var formattedContent = File.ReadAllText(file);
                File.WriteAllText(output, FormatOutputDiffers ? $"{formattedContent}// formatted\n" : formattedContent);
            }

            if (string.Equals(FailedCommand, command[0], StringComparison.Ordinal))
            {
                return Task.FromResult(new BicepCommandResult(1, string.Empty, "command failed"));
            }

            if (LintWarning && command[0] == "lint")
            {
                return Task.FromResult(new BicepCommandResult(0, string.Empty, "Warning BCP999: sample warning"));
            }

            return Task.FromResult(CompiledOutputContainsWarning && command[0] is "build" or "build-params"
                ? new BicepCommandResult(0, "{\"metadata\":{\"description\":\"disk warning\"}}", string.Empty)
                : new BicepCommandResult(0, string.Empty, string.Empty));
        }
    }

}
