namespace InfraFlowSculptor.Infrastructure.Tests.Jobs;

public sealed class PowerShellFactAttribute : FactAttribute
{
    public PowerShellFactAttribute()
    {
        var executableName = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";
        var pathEntries = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        var hasPowerShell = pathEntries
            .Select(pathEntry => pathEntry.Trim().Trim('"'))
            .Where(directory => !string.IsNullOrWhiteSpace(directory) && Path.IsPathFullyQualified(directory))
            .Any(directory => File.Exists(Path.Combine(directory, executableName)));
        if (!hasPowerShell)
        {
            Skip = "This process-tree integration test requires PowerShell 7 (pwsh) on PATH.";
        }
    }
}
