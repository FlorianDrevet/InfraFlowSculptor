using InfraFlowSculptor.Application.Common.Notifications;

namespace InfraFlowSculptor.Application.Tests.Notifications;

public sealed class InvitationTokenGeneratorTests
{
    [Fact]
    public void GenerateReturnsUrlSafeEncodingOf256RandomBits()
    {
        var token = InvitationTokenGenerator.Generate();

        Assert.Equal(43, token.Length);
        Assert.DoesNotContain("+", token, StringComparison.Ordinal);
        Assert.DoesNotContain("/", token, StringComparison.Ordinal);
        Assert.DoesNotContain("=", token, StringComparison.Ordinal);
        var bytes = Convert.FromBase64String(token.Replace('-', '+').Replace('_', '/') + "=");
        Assert.Equal(32, bytes.Length);
    }
}
