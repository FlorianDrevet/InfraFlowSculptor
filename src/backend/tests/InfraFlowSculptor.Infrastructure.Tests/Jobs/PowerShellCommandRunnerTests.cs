using System.Diagnostics;
using InfraFlowSculptor.Worker.Jobs;

namespace InfraFlowSculptor.Infrastructure.Tests.Jobs;

public sealed class PowerShellCommandRunnerTests
{
    [PowerShellFact]
    public async Task RunAsyncKillsTheProcessTreeWhenCanceled()
    {
        using var directory = new TemporaryDirectory("ifs-powershell-cancel-");
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
        using var cancellation = new CancellationTokenSource();
        var execution = PowerShellCommandRunner.RunAsync(
            directory.FullName,
            ["-NoProfile", "-File", script, pidFile, grandchildPidFile],
            cancellation.Token);
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
                "The PowerShell child process was left running after cancellation.");
            Assert.True(await WaitForProcessExitAsync(grandchild, TimeSpan.FromSeconds(3)),
                "The PowerShell grandchild process was left running after cancellation.");
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
