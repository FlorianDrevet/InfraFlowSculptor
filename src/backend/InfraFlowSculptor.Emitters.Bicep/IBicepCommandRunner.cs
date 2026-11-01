namespace InfraFlowSculptor.Emitters.Bicep;

internal interface IBicepCommandRunner
{
    Task<BicepCommandResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}
