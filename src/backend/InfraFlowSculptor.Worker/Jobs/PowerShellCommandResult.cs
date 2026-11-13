namespace InfraFlowSculptor.Worker.Jobs;

internal sealed record PowerShellCommandResult(int ExitCode, string StandardOutput, string StandardError);
