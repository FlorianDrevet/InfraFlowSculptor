using System.ComponentModel;
using System.Diagnostics;
using InfraFlowSculptor.Emitters.Bicep;

namespace InfraFlowSculptor.Emitters.Tests;

public sealed class BicepCommandRunnerTests
{
    private const string AzureCliInstallerEnvironmentVariable = "AZ_INSTALLER";
    private const string AzureCliMsiInstallerValue = "MSI";

    [Fact]
    public async Task RunAsyncUsesAzureCliInstalledBicepWhenStandaloneIsUnavailable()
    {
        var invocations = new List<ProcessStartInfo>();
        var runner = new BicepCommandRunner((startInfo, _) =>
        {
            invocations.Add(startInfo);
            return startInfo.FileName == "bicep"
                ? Task.FromException<BicepCommandResult>(new Win32Exception(2, "not found"))
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
                    ".azure", "bin", OperatingSystem.IsWindows() ? "bicep.exe" : "bicep"), azureManagedBicep.FileName);
                Assert.Equal(["--version"], azureManagedBicep.ArgumentList);
            });
    }

    [Fact]
    public async Task RunAsyncUsesAzureCliVersionSubcommandWhenOtherBicepExecutablesAreUnavailable()
    {
        var invocations = new List<ProcessStartInfo>();
        var azureCliExecutable = (Executable: "az", PrefixArguments: (IReadOnlyList<string>)Array.Empty<string>());
        var azureManagedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".azure", "bin", OperatingSystem.IsWindows() ? "bicep.exe" : "bicep");
        var runner = new BicepCommandRunner((startInfo, _) =>
        {
            invocations.Add(startInfo);
            return startInfo.FileName == azureCliExecutable.Executable
                ? Task.FromResult(new BicepCommandResult(0, "Bicep CLI version 0.47.16", string.Empty))
                : Task.FromException<BicepCommandResult>(new Win32Exception(2, "not found"));
        }, () => azureCliExecutable);

        var result = await runner.RunAsync(".", ["--version"], CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Collection(invocations,
            standalone => Assert.Equal("bicep", standalone.FileName),
            azureManagedBicep => Assert.Equal(azureManagedPath, azureManagedBicep.FileName),
            azureCli =>
            {
                Assert.Equal(azureCliExecutable.Executable, azureCli.FileName);
                Assert.Equal(["bicep", "version"], azureCli.ArgumentList);
            });
    }

    [Fact]
    public async Task RunAsyncReportsAllStartErrorsWhenNoExecutableCanStart()
    {
        var invocations = new List<string>();
        var runner = new BicepCommandRunner((startInfo, _) =>
        {
            invocations.Add(startInfo.FileName);
            return Task.FromException<BicepCommandResult>(new Win32Exception(2, "not found"));
        }, () => ("az", Array.Empty<string>()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await runner.RunAsync(".", ["--version"], CancellationToken.None));

        var aggregateException = Assert.IsType<AggregateException>(exception.InnerException);
        Assert.Equal(3, aggregateException.InnerExceptions.Count);
        Assert.Equal(
            ["bicep", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".azure", "bin", OperatingSystem.IsWindows() ? "bicep.exe" : "bicep"), "az"],
            invocations);
    }

    [Fact]
    public async Task RunAsyncDoesNotTreatAccessDeniedAsAnUnavailableExecutable()
    {
        var invocations = new List<string>();
        var runner = new BicepCommandRunner((startInfo, _) =>
        {
            invocations.Add(startInfo.FileName);
            return Task.FromException<BicepCommandResult>(new Win32Exception(5, "Access is denied."));
        }, () => ("az", Array.Empty<string>()));

        var exception = await Assert.ThrowsAsync<Win32Exception>(async () =>
            await runner.RunAsync(".", ["--version"], CancellationToken.None));

        Assert.Equal(5, exception.NativeErrorCode);
        Assert.Equal(["bicep"], invocations);
    }

    [Fact]
    public async Task RunAsyncDoesNotTryAnotherExecutableAfterBicepReturnsFailure()
    {
        var invocations = new List<string>();
        var runner = new BicepCommandRunner((startInfo, _) =>
        {
            invocations.Add(startInfo.FileName);
            return Task.FromResult(new BicepCommandResult(17, string.Empty, "Bicep rejected the file."));
        }, () => ("az", Array.Empty<string>()));

        var result = await runner.RunAsync(".", ["build", "main.bicep"], CancellationToken.None);

        Assert.Equal(17, result.ExitCode);
        Assert.Equal(["bicep"], invocations);
    }

    [Fact]
    public async Task RunAsyncKeepsAzureCliUpdateNoticeFromStandaloneBicep()
    {
        const string updateNotice = "WARNING: A new Bicep release is available: v0.49.2. Upgrade now.";
        var runner = new BicepCommandRunner((startInfo, _) => startInfo.FileName == "bicep"
            ? Task.FromResult(new BicepCommandResult(0, string.Empty, updateNotice))
            : Task.FromException<BicepCommandResult>(new Win32Exception(2, "not found")),
            () => null);

        var result = await runner.RunAsync(".", ["--version"], CancellationToken.None);

        Assert.Equal(updateNotice, result.StandardError);
    }

    [Fact]
    public void ResolveAzureCliInvocationRunsTheBundledPythonInsteadOfTheWindowsCommandScript()
    {
        using var directory = new TemporaryDirectory("ifs-az-path-");
        var wbin = Path.Combine(directory.FullName, "wbin");
        var azureBin = Path.Combine(directory.FullName, ".azure", "bin");
        Directory.CreateDirectory(wbin);
        Directory.CreateDirectory(azureBin);
        File.WriteAllText(Path.Combine(wbin, "az.cmd"), string.Empty);
        File.WriteAllText(Path.Combine(azureBin, "bicep.exe"), string.Empty);
        var bundledPython = Path.Combine(directory.FullName, "python.exe");
        File.WriteAllText(bundledPython, string.Empty);

        var invocation = BicepCommandRunner.ResolveAzureCliInvocation(
            directory.FullName,
            [$"\"{wbin}\""],
            isWindows: true);

        Assert.NotNull(invocation);
        Assert.Equal(Path.GetFullPath(bundledPython), invocation.Value.Executable);
        Assert.Equal(["-IBm", "azure.cli"], invocation.Value.PrefixArguments);
    }

    [Fact]
    public void ResolveAzureCliInvocationPrefersAnExecutableOverTheWindowsCommandScript()
    {
        using var directory = new TemporaryDirectory("ifs-az-path-");
        var wbin = Path.Combine(directory.FullName, "wbin");
        var nativeBin = Path.Combine(directory.FullName, "native");
        var azureBin = Path.Combine(directory.FullName, ".azure", "bin");
        Directory.CreateDirectory(wbin);
        Directory.CreateDirectory(nativeBin);
        Directory.CreateDirectory(azureBin);
        File.WriteAllText(Path.Combine(wbin, "az.cmd"), string.Empty);
        File.WriteAllText(Path.Combine(directory.FullName, "python.exe"), string.Empty);
        File.WriteAllText(Path.Combine(azureBin, "bicep.exe"), string.Empty);
        var nativeExecutable = Path.Combine(nativeBin, "az.exe");
        File.WriteAllText(nativeExecutable, string.Empty);

        var invocation = BicepCommandRunner.ResolveAzureCliInvocation(
            directory.FullName,
            [wbin, nativeBin],
            isWindows: true);

        Assert.NotNull(invocation);
        Assert.Equal(Path.GetFullPath(nativeExecutable), invocation.Value.Executable);
        Assert.Empty(invocation.Value.PrefixArguments);
    }

    [Fact]
    public void ResolveAzureCliExecutableIgnoresRelativePathEntries()
    {
        var relativeDirectory = $"ifs-relative-az-{Guid.NewGuid():N}";
        Directory.CreateDirectory(relativeDirectory);
        try
        {
            File.WriteAllText(Path.Combine(relativeDirectory, "az.exe"), string.Empty);

            var invocation = BicepCommandRunner.ResolveAzureCliExecutable(
                [relativeDirectory],
                isWindows: true);

            Assert.Null(invocation);
        }
        finally
        {
            Directory.Delete(relativeDirectory, recursive: true);
        }
    }

    [Fact]
    public void ResolveAzureCliInvocationIgnoresAWindowsCommandScriptWithoutBundledPython()
    {
        using var directory = new TemporaryDirectory("ifs-az-path-");
        var wbin = Path.Combine(directory.FullName, "wbin");
        var azureBin = Path.Combine(directory.FullName, ".azure", "bin");
        Directory.CreateDirectory(wbin);
        Directory.CreateDirectory(azureBin);
        File.WriteAllText(Path.Combine(wbin, "az.cmd"), string.Empty);

        var invocation = BicepCommandRunner.ResolveAzureCliInvocation(
            directory.FullName,
            [wbin],
            isWindows: true);

        Assert.Null(invocation);
    }

    [Fact]
    public void ResolveAzureCliInvocationRequiresPinnedBicepBeforeUsingAzureCli()
    {
        using var directory = new TemporaryDirectory("ifs-az-path-");
        var wbin = Path.Combine(directory.FullName, "wbin");
        Directory.CreateDirectory(wbin);
        File.WriteAllText(Path.Combine(wbin, "az.cmd"), string.Empty);
        File.WriteAllText(Path.Combine(directory.FullName, "python.exe"), string.Empty);

        var invocation = BicepCommandRunner.ResolveAzureCliInvocation(
            directory.FullName,
            [wbin],
            isWindows: true);

        Assert.Null(invocation);
    }

    [AzureCliBicepFact]
    public async Task RunAsyncUsesAzureCliFallbackWhenStandaloneBicepExecutablesAreUnavailable()
    {
        var azureCliInvocation = BicepCommandRunner.GetAzureCliInvocation();
        Assert.NotNull(azureCliInvocation);
        var runner = new BicepCommandRunner((startInfo, cancellationToken) =>
            startInfo.FileName == azureCliInvocation.Value.Executable
                ? BicepCommandRunner.ExecuteProcessAsync(startInfo, cancellationToken)
                : Task.FromException<BicepCommandResult>(new Win32Exception(2, "not found")),
            () => azureCliInvocation);

        var result = await runner.RunAsync(Environment.CurrentDirectory, ["--version"], CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("Bicep CLI version ", result.StandardOutput.Trim(), StringComparison.Ordinal);
        Assert.DoesNotContain("new Bicep release is available", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsyncSetsMsiInstallerEnvironmentForBundledAzureCliPython()
    {
        var invocations = new List<ProcessStartInfo>();
        var azureCliInvocation = (
            Executable: "python.exe",
            PrefixArguments: (IReadOnlyList<string>)["-IBm", "azure.cli"]);
        var runner = new BicepCommandRunner((startInfo, _) =>
        {
            if (startInfo.FileName != azureCliInvocation.Executable)
            {
                return Task.FromException<BicepCommandResult>(new Win32Exception(2, "not found"));
            }

            invocations.Add(startInfo);
            return Task.FromResult(new BicepCommandResult(0, "Bicep CLI version 0.47.16", string.Empty));
        }, () => azureCliInvocation);

        await runner.RunAsync(".", ["--version"], CancellationToken.None);

        var python = Assert.Single(invocations);
        Assert.Equal(["-IBm", "azure.cli", "bicep", "version"], python.ArgumentList);
        Assert.Equal(AzureCliMsiInstallerValue, python.Environment[AzureCliInstallerEnvironmentVariable]);
    }

    [Fact]
    public async Task RunAsyncDoesNotOverrideInstallerEnvironmentForNativeAzureCli()
    {
        var invocations = new List<ProcessStartInfo>();
        var inheritedInstallerValue = Environment.GetEnvironmentVariable(AzureCliInstallerEnvironmentVariable);
        const string azureCliExecutable = "az.exe";
        var runner = new BicepCommandRunner((startInfo, _) =>
        {
            if (startInfo.FileName != azureCliExecutable)
            {
                return Task.FromException<BicepCommandResult>(new Win32Exception(2, "not found"));
            }

            invocations.Add(startInfo);
            return Task.FromResult(new BicepCommandResult(0, "Bicep CLI version 0.47.16", string.Empty));
        }, () => (azureCliExecutable, Array.Empty<string>()));

        await runner.RunAsync(".", ["--version"], CancellationToken.None);

        var nativeAzureCli = Assert.Single(invocations);
        var hasInstallerValue = nativeAzureCli.Environment.TryGetValue(
            AzureCliInstallerEnvironmentVariable,
            out var actualInstallerValue);
        Assert.Equal(inheritedInstallerValue is not null, hasInstallerValue);
        Assert.Equal(inheritedInstallerValue, actualInstallerValue);
    }

    [AzureCliBicepFact]
    public async Task RunAsyncBuildsLintsFormatsAndCompilesParametersThroughAzureCliFallback()
    {
        var azureCliInvocation = BicepCommandRunner.GetAzureCliInvocation();
        Assert.NotNull(azureCliInvocation);
        var runner = new BicepCommandRunner((startInfo, cancellationToken) =>
            startInfo.FileName == azureCliInvocation.Value.Executable
                ? BicepCommandRunner.ExecuteProcessAsync(startInfo, cancellationToken)
                : Task.FromException<BicepCommandResult>(new Win32Exception(2, "not found")),
            () => azureCliInvocation);
        var repositoryRoot = FindRepositoryRoot();
        var infrastructureDirectory = Path.Combine(
            repositoryRoot,
            "reference",
            "pilot",
            "bicep-azdo",
            "orders",
            "infra");
        var mainFile = Path.Combine(infrastructureDirectory, "main.bicep");
        var parametersFile = Path.Combine(infrastructureDirectory, "main.prd.bicepparam");
        using var outputDirectory = new TemporaryDirectory("ifs-az-format-");
        var formattedFile = Path.Combine(outputDirectory.FullName, "main.bicep");
        string[][] commands =
        [
            ["build", mainFile, "--stdout"],
            ["lint", mainFile],
            ["build-params", parametersFile, "--stdout"],
            ["format", mainFile, "--outfile", formattedFile, "--newline-kind", "LF", "--insert-final-newline"]
        ];

        foreach (var command in commands)
        {
            var result = await runner.RunAsync(repositoryRoot, command, CancellationToken.None);
            Assert.True(result.ExitCode == 0,
                $"Azure CLI Bicep {command[0]} failed with exit code {result.ExitCode}: {result.StandardError}");
            if (command[0] is "build" or "build-params")
            {
                Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput));
            }
        }

        Assert.True(File.Exists(formattedFile));
        Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(formattedFile)));
    }

    [Fact]
    public async Task RunAsyncTranslatesAzureCliArgumentsForBuildAndValidationCommands()
    {
        var scenarios = new[]
        {
            (Arguments: new[] { "build", "main.bicep", "--stdout" },
                Expected: new[] { "bicep", "build", "--file", "main.bicep", "--stdout" }),
            (Arguments: new[] { "lint", "main.bicep", "--no-restore" },
                Expected: new[] { "bicep", "lint", "--file", "main.bicep", "--no-restore" }),
            (Arguments: new[] { "build-params", "main.bicepparam", "--stdout" },
                Expected: new[] { "bicep", "build-params", "--file", "main.bicepparam", "--stdout" }),
            (Arguments: new[] { "format", "main.bicep", "--newline-kind", "LF", "--insert-final-newline", "--stdout" },
                Expected: new[] { "bicep", "format", "--file", "main.bicep", "--newline-kind", "LF", "--insert-final-newline", "--stdout" })
        };

        foreach (var scenario in scenarios)
        {
            var azureCliInvocations = new List<ProcessStartInfo>();
            const string azureCliExecutable = "az";
            var runner = new BicepCommandRunner((startInfo, _) =>
            {
                if (startInfo.FileName != azureCliExecutable)
                {
                    return Task.FromException<BicepCommandResult>(new Win32Exception(2, "not found"));
                }

                azureCliInvocations.Add(startInfo);
                return Task.FromResult(new BicepCommandResult(0, string.Empty, string.Empty));
            }, () => (azureCliExecutable, Array.Empty<string>()));

            await runner.RunAsync(".", scenario.Arguments, CancellationToken.None);

            Assert.Equal(scenario.Expected, Assert.Single(azureCliInvocations).ArgumentList);
        }
    }

    [Fact]
    public async Task RunAsyncDoesNotTryAnotherExecutableAfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var invocations = new List<string>();
        var runner = new BicepCommandRunner((startInfo, _) =>
        {
            invocations.Add(startInfo.FileName);
            cancellation.Cancel();
            return Task.FromException<BicepCommandResult>(new Win32Exception(2, "process termination failed"));
        }, () => null);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await runner.RunAsync(".", ["--version"], cancellation.Token));

        Assert.Single(invocations);
    }

    [Fact]
    public async Task RunAsyncFiltersAzureCliUpdateNoticeButKeepsBicepDiagnostics()
    {
        const string azureCliUpdateNotice = "WARNING: A new Bicep release is available: v0.49.2. Upgrade now.";
        const string bicepWarning = "C:\\output.bicep(2,7) : Warning no-unused-params: Parameter is unused.";
        const string azureCliExecutable = "az";
        var runner = new BicepCommandRunner((startInfo, _) => startInfo.FileName == azureCliExecutable
            ? Task.FromResult(new BicepCommandResult(
                0,
                string.Empty,
                $"{azureCliUpdateNotice}{Environment.NewLine}{bicepWarning}{Environment.NewLine}"))
            : Task.FromException<BicepCommandResult>(new Win32Exception(2, "not found")),
            () => (azureCliExecutable, Array.Empty<string>()));

        var result = await runner.RunAsync(".", ["lint", "main.bicep"], CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.DoesNotContain("new Bicep release is available", result.StandardError, StringComparison.Ordinal);
        Assert.Contains(bicepWarning, result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsyncFiltersAzureCliUpdateNoticeAtEndOfOutput()
    {
        const string azureCliUpdateNotice = "WARNING: A new Bicep release is available: v0.49.2. Upgrade now.";
        const string azureCliExecutable = "az";
        var runner = new BicepCommandRunner((startInfo, _) => startInfo.FileName == azureCliExecutable
            ? Task.FromResult(new BicepCommandResult(0, string.Empty, azureCliUpdateNotice))
            : Task.FromException<BicepCommandResult>(new Win32Exception(2, "not found")),
            () => (azureCliExecutable, Array.Empty<string>()));

        var result = await runner.RunAsync(".", ["--version"], CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardError);
    }

    [PowerShellFact]
    public async Task ExecuteProcessAsyncKillsTheProcessTreeWhenCanceled()
    {
        using var directory = new TemporaryDirectory("ifs-bicep-cancel-");
        var pidFile = Path.Combine(directory.FullName, "child.pid");
        var grandchildPidFile = Path.Combine(directory.FullName, "grandchild.pid");
        var script = Path.Combine(directory.FullName, "sleep.ps1");
        await File.WriteAllTextAsync(script, """
            $pidPath = $args[0]
            $grandchildPidPath = $args[1]
            $pidTempPath = "$pidPath.tmp"
            [IO.File]::WriteAllText($pidTempPath, [string]$PID)
            [IO.File]::Move($pidTempPath, $pidPath)
            $childStartInfo = [System.Diagnostics.ProcessStartInfo]::new('pwsh')
            $childStartInfo.UseShellExecute = $false
            $childStartInfo.CreateNoWindow = $true
            $childStartInfo.ArgumentList.Add('-NoProfile')
            $childStartInfo.ArgumentList.Add('-Command')
            $childStartInfo.ArgumentList.Add('Start-Sleep -Seconds 30')
            $grandchild = [System.Diagnostics.Process]::Start($childStartInfo)
            $grandchildTempPath = "$grandchildPidPath.tmp"
            [IO.File]::WriteAllText($grandchildTempPath, [string]$grandchild.Id)
            [IO.File]::Move($grandchildTempPath, $grandchildPidPath)
            Start-Sleep -Seconds 30
            """);
        var startInfo = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = directory.FullName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add(pidFile);
        startInfo.ArgumentList.Add(grandchildPidFile);
        using var cancellation = new CancellationTokenSource();
        var execution = BicepCommandRunner.ExecuteProcessAsync(startInfo, cancellation.Token);
        Process? child = null;
        Process? grandchild = null;
        try
        {
            child = await ReadPowerShellProcessAsync(pidFile);
            grandchild = await ReadPowerShellProcessAsync(grandchildPidFile);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await execution.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.True(await WaitForProcessExitAsync(child, TimeSpan.FromSeconds(3)),
                "The Bicep child process was left running after cancellation.");
            Assert.True(await WaitForProcessExitAsync(grandchild, TimeSpan.FromSeconds(3)),
                "The Bicep grandchild process was left running after cancellation.");
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                await execution.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
            }

            await StopPowerShellProcessAsync(child);
            await StopPowerShellProcessAsync(grandchild);
        }
    }

    private static async Task<Process> ReadPowerShellProcessAsync(string path)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(15))
        {
            if (File.Exists(path))
            {
                Process? process = null;
                try
                {
                    if (int.TryParse(await File.ReadAllTextAsync(path), out var processId) && processId > 0)
                    {
                        process = Process.GetProcessById(processId);
                        if (process.ProcessName == "pwsh")
                        {
                            var result = process;
                            process = null;
                            return result;
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (ArgumentException)
                {
                }
                catch (InvalidOperationException)
                {
                }
                finally
                {
                    process?.Dispose();
                }
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("The child process did not record its process ID.");
    }

    private static async Task<bool> WaitForProcessExitAsync(Process process, TimeSpan timeout)
    {
        try
        {
            await process.WaitForExitAsync().WaitAsync(timeout);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static async Task StopPowerShellProcessAsync(Process? process)
    {
        if (process is null)
        {
            return;
        }

        using (process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
            }
            catch (System.ComponentModel.Win32Exception) when (process.HasExited)
            {
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "reference", "pilot", "bicep-azdo")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The repository root could not be found from the test output directory.");
    }

    private sealed class TemporaryDirectory(string prefix) : IDisposable
    {
        public string FullName { get; } = Directory.CreateTempSubdirectory(prefix).FullName;

        public void Dispose()
        {
            if (Directory.Exists(FullName))
            {
                Directory.Delete(FullName, recursive: true);
            }
        }
    }
}
