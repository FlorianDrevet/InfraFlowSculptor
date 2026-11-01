using System.ComponentModel;
using System.Diagnostics;
using InfraFlowSculptor.Emitters.Bicep;

namespace InfraFlowSculptor.Emitters.Tests;

public sealed class BicepCommandRunnerTests
{
    [Fact]
    public async Task RunAsyncUsesAzureCliInstalledBicepWhenStandaloneIsUnavailable()
    {
        var invocations = new List<ProcessStartInfo>();
        var runner = new BicepCommandRunner((startInfo, _) =>
        {
            invocations.Add(startInfo);
            return startInfo.FileName == "bicep"
                ? Task.FromException<BicepCommandResult>(new Win32Exception("not found"))
                : Task.FromResult(new BicepCommandResult(0, "Bicep CLI version 0.47.16", string.Empty));
        });

        var result = await runner.RunAsync(".", ["--version"], CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Collection(invocations,
            standalone =>
            {
                Assert.Equal("bicep", standalone.FileName);
                Assert.Equal(["--version"], standalone.ArgumentList);
            },
            azureManagedBicep =>
            {
                Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".azure", "bin", "bicep.exe"), azureManagedBicep.FileName);
                Assert.Equal(["--version"], azureManagedBicep.ArgumentList);
            });
    }
}
