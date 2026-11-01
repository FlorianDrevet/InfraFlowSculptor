using System.ComponentModel;
using System.Diagnostics;

namespace InfraFlowSculptor.Emitters.Bicep;

internal sealed class BicepCommandRunner : IBicepCommandRunner
{
    private readonly Func<ProcessStartInfo, CancellationToken, Task<BicepCommandResult>> executeProcess;

    public BicepCommandRunner() : this(ExecuteProcessAsync)
    {
    }

    internal BicepCommandRunner(Func<ProcessStartInfo, CancellationToken, Task<BicepCommandResult>> executeProcess)
    {
        ArgumentNullException.ThrowIfNull(executeProcess);
        this.executeProcess = executeProcess;
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
            "bicep.exe");
        var azureCliArguments = new string[arguments.Count + 1];
        azureCliArguments[0] = "bicep";
        for (var index = 0; index < arguments.Count; index++)
        {
            azureCliArguments[index + 1] = arguments[index];
        }

        var candidates = new (string Executable, IReadOnlyList<string> Arguments)[]
        {
            ("bicep", arguments),
            (azureBicepPath, arguments),
            ("az", azureCliArguments)
        };
        var startErrors = new List<Win32Exception>(candidates.Length);

        foreach (var candidate in candidates)
        {
            try
            {
                return await executeProcess(
                    CreateStartInfo(candidate.Executable, workingDirectory, candidate.Arguments), cancellationToken);
            }
            catch (Win32Exception exception)
            {
                startErrors.Add(exception);
            }
        }

        throw new InvalidOperationException(
            "The pinned Bicep CLI is not available on PATH or in the Azure CLI user installation.",
            new AggregateException(startErrors));
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

    private static async Task<BicepCommandResult> ExecuteProcessAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start the '{startInfo.FileName}' CLI process.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new BicepCommandResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }
}
