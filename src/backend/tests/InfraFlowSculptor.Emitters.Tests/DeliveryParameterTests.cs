using InfraFlowSculptor.Emitters.Bicep;

namespace InfraFlowSculptor.Emitters.Tests;

public sealed class DeliveryParameterTests
{
    [Fact]
    public void WriteDeliveryParameterEscapesCatalogDescriptionForBicep()
    {
        var writer = new BicepWriter();

        MainFileEmitter.WriteDeliveryParameter(
            writer,
            "appDeliveryPrincipalId",
            "L'identité ${principal}\\path\nseconde ligne");

        Assert.Equal(
            "\n@description('L\\'identité \\${principal}\\\\path\\nseconde ligne')\n"
                + "param appDeliveryPrincipalId string = ''\n",
            writer.ToString());
    }
}
