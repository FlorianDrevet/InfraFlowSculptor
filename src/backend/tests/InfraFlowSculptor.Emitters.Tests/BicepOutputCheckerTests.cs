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
        var catalog = CatalogLoader.Load().GetVersion("2026.10-pilot.6");
        Assert.NotNull(catalog);
        var plan = PilotReferenceModel.Create(catalog!);
        var orders = Assert.Single(plan.Components, component => component.ComponentCode == "orders");
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
        Assert.NotNull(orders);

        var emitter = new BicepInfrastructureEmitter(
            new ModuleSourceStrategyRegistry([new PublicAvmModuleSourceStrategy()]));
        var files = emitter.Emit(plan, catalog!);
        var checker = new BicepOutputChecker();

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
                return Task.FromResult(new BicepCommandResult(0, "Bicep CLI version 0.47.16", string.Empty));
            }

            if (command[0] == "format")
            {
                var file = Path.Combine(workingDirectory, command[1]);
                var output = Path.Combine(workingDirectory, command[Array.IndexOf(command, "--outfile") + 1]);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllText(output, File.ReadAllText(file));
            }

            return Task.FromResult(LintWarning && command[0] == "lint"
                ? new BicepCommandResult(0, "Warning BCP999: sample warning", string.Empty)
                : new BicepCommandResult(0, string.Empty, string.Empty));
        }
    }
}
