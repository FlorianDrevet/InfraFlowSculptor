namespace InfraFlowSculptor.Emitters.Tests;

public sealed class BicepCliFactAttribute : FactAttribute
{
    public BicepCliFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFS_RUN_BICEP_CLI_TESTS"), "1", StringComparison.Ordinal))
        {
            Skip = "Set IFS_RUN_BICEP_CLI_TESTS=1 to run the Bicep CLI integration test.";
        }
    }
}
