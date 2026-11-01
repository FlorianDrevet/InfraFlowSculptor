namespace InfraFlowSculptor.Emitters.Bicep;

internal sealed record BicepCommandResult(int ExitCode, string StandardOutput, string StandardError);
