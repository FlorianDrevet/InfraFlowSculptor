using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace InfraFlowSculptor.Emitters.Bicep;

internal sealed class BicepCommandRunner : IBicepCommandRunner
{
    private const string AzureCliInstallerEnvironmentVariable = "AZ_INSTALLER";
    private const string AzureCliMsiInstallerValue = "MSI";
    private const int FileNotFoundErrorCode = 2;
    private const int PathNotFoundErrorCode = 3;
    private static readonly string[] AzureCliVersionArguments = ["--version"];
    private static readonly string[] AzureCliVersionCommand = ["bicep", "version"];
    private static readonly string[] AzureCliPythonPrefixArguments = ["-IBm", "azure.cli"];
    private static readonly Regex AzureCliBicepUpdateNoticePattern = new(
        @"(?im)^WARNING: A new Bicep release is available:[^\r\n]*(?:\r?\n|$)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly HashSet<string> AzureCliFileCommands = new(StringComparer.Ordinal)
    {
        "build",
        "build-params",
        "format",
        "lint"
    };
    private readonly Func<ProcessStartInfo, CancellationToken, Task<BicepCommandResult>> executeProcess;
    private readonly Func<(string Executable, IReadOnlyList<string> PrefixArguments)?> getAzureCliInvocation;

    public BicepCommandRunner() : this(ExecuteProcessAsync, GetAzureCliInvocation)
    {
    }

    internal BicepCommandRunner(Func<ProcessStartInfo, CancellationToken, Task<BicepCommandResult>> executeProcess)
        : this(executeProcess, GetAzureCliInvocation)
    {
    }

    internal BicepCommandRunner(
        Func<ProcessStartInfo, CancellationToken, Task<BicepCommandResult>> executeProcess,
        Func<(string Executable, IReadOnlyList<string> PrefixArguments)?> getAzureCliInvocation)
    {
        ArgumentNullException.ThrowIfNull(executeProcess);
        ArgumentNullException.ThrowIfNull(getAzureCliInvocation);
        this.executeProcess = executeProcess;
        this.getAzureCliInvocation = getAzureCliInvocation;
    }

    public async Task<BicepCommandResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);

        var azureBicepPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".azure",
            "bin",
            OperatingSystem.IsWindows() ? "bicep.exe" : "bicep");
        IReadOnlyList<string> azureCliArguments;
        if (arguments.SequenceEqual(AzureCliVersionArguments))
        {
            azureCliArguments = AzureCliVersionCommand;
        }
        else
        {
            azureCliArguments = TranslateAzureCliArguments(arguments);
        }

        var candidates = new List<(string Executable, IReadOnlyList<string> Arguments, bool IsAzureCli)>
        {
            ("bicep", arguments, false),
            (azureBicepPath, arguments, false)
        };
        // Keep Azure CLI last for managed installations that can resolve the preinstalled pinned binary when direct launch is unavailable.
        if (getAzureCliInvocation() is { } azureCliInvocation)
        {
            candidates.Add((
                azureCliInvocation.Executable,
                azureCliInvocation.PrefixArguments.Concat(azureCliArguments).ToArray(),
                true));
        }

        var startErrors = new List<Win32Exception>(candidates.Count);

        foreach (var candidate in candidates)
        {
            try
            {
                var startInfo = CreateStartInfo(candidate.Executable, workingDirectory, candidate.Arguments);
                if (candidate.IsAzureCli && candidate.Arguments.Take(AzureCliPythonPrefixArguments.Length)
                    .SequenceEqual(AzureCliPythonPrefixArguments))
                {
                    startInfo.Environment[AzureCliInstallerEnvironmentVariable] = AzureCliMsiInstallerValue;
                }

                var result = await executeProcess(
                    startInfo,
                    cancellationToken);
                return candidate.IsAzureCli
                    ? result with
                    {
                        StandardError = AzureCliBicepUpdateNoticePattern.Replace(result.StandardError, string.Empty)
                    }
                    : result;
            }
            catch (Win32Exception exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (exception.NativeErrorCode is not (FileNotFoundErrorCode or PathNotFoundErrorCode))
                {
                    throw;
                }

                startErrors.Add(exception);
            }
        }

        throw new InvalidOperationException(
            "The pinned Bicep CLI is not available on PATH or in the Azure CLI user installation.",
            new AggregateException(startErrors));
    }

    internal static (string Executable, IReadOnlyList<string> PrefixArguments)? GetAzureCliInvocation()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var pathEntries = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        return ResolveAzureCliInvocation(userProfile, pathEntries, OperatingSystem.IsWindows());
    }

    internal static (string Executable, IReadOnlyList<string> PrefixArguments)? ResolveAzureCliInvocation(
        string userProfile,
        IEnumerable<string> pathEntries,
        bool isWindows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userProfile);
        ArgumentNullException.ThrowIfNull(pathEntries);
        var pinnedBicepExecutable = Path.Combine(
            userProfile,
            ".azure",
            "bin",
            isWindows ? "bicep.exe" : "bicep");
        // `az bicep` may install Bicep on first use; only allow the fallback if the pinned CLI is already installed.
        if (!File.Exists(pinnedBicepExecutable))
        {
            return null;
        }

        return ResolveAzureCliExecutable(pathEntries, isWindows);
    }

    internal static (string Executable, IReadOnlyList<string> PrefixArguments)? ResolveAzureCliExecutable(
        IEnumerable<string> pathEntries,
        bool isWindows)
    {
        ArgumentNullException.ThrowIfNull(pathEntries);
        if (!isWindows)
        {
            return ("az", Array.Empty<string>());
        }

        var directories = pathEntries
            .Select(pathEntry => pathEntry.Trim().Trim('"'))
            .Where(directory => Path.IsPathFullyQualified(directory) && Directory.Exists(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var directory in directories)
        {
            foreach (var executableName in new[] { "az.exe" })
            {
                var executablePath = Path.Combine(directory, executableName);
                if (File.Exists(executablePath))
                {
                    return (Path.GetFullPath(executablePath), Array.Empty<string>());
                }
            }
        }

        foreach (var directory in directories)
        {
            var commandScript = Path.Combine(directory, "az.cmd");
            var bundledPython = Path.GetFullPath(Path.Combine(directory, "..", "python.exe"));
            if (File.Exists(commandScript) && File.Exists(bundledPython))
            {
                return (bundledPython, AzureCliPythonPrefixArguments);
            }
        }

        return null;
    }

    private static List<string> TranslateAzureCliArguments(IReadOnlyList<string> arguments)
    {
        if (arguments.Count >= 2 && AzureCliFileCommands.Contains(arguments[0]))
        {
            var translated = new List<string>(arguments.Count + 2)
            {
                "bicep",
                arguments[0],
                "--file",
                arguments[1]
            };
            translated.AddRange(arguments.Skip(2));
            return translated;
        }

        var commandArguments = new List<string>(arguments.Count + 1) { "bicep" };
        commandArguments.AddRange(arguments);
        return commandArguments;
    }

    private static ProcessStartInfo CreateStartInfo(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    internal static async Task<BicepCommandResult> ExecuteProcessAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start the '{startInfo.FileName}' CLI process.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
            }
            catch (Win32Exception) when (process.HasExited)
            {
            }

            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(standardOutput, standardError);
            throw;
        }

        return new BicepCommandResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }
}
