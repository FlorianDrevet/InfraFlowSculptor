using InfraFlowSculptor.Emitters.Bicep;

namespace InfraFlowSculptor.Emitters.Tests;

public sealed class AzureCliBicepFactAttribute : FactAttribute
{
    public AzureCliBicepFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFS_RUN_AZURE_BICEP_CLI_TESTS"), "1", StringComparison.Ordinal))
        {
            Skip = "Set IFS_RUN_AZURE_BICEP_CLI_TESTS=1 to run the Azure CLI Bicep integration test.";
        }
        else if (BicepCommandRunner.GetAzureCliInvocation() is null)
        {
            Skip = "A directly invokable Azure CLI Bicep installation is not available.";
        }
    }
}
