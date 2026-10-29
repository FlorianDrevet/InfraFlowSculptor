using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Domain.Tests.ComponentAggregate;

public sealed class SecretValueDetectorTests
{
    [Fact]
    public void RGPAR05KnownGitHubTokenIsSuspicious()
    {
        var value = $"ghp_{new string('a', 36)}";

        Assert.True(SecretValueDetector.IsSuspicious(value));
    }

    [Fact]
    public void RGPAR05ConnectionStringWithPasswordIsSuspicious()
    {
        const string value = "Server=tcp:database.example;Database=shop;User Id=app;Password=fake-password";

        Assert.True(SecretValueDetector.IsSuspicious(value));
    }

    [Fact]
    public void RGPAR05HighEntropyValueOfTwentyCharactersIsSuspicious()
    {
        var value = new string(Enumerable.Range(33, 20).Select(character => (char)character).ToArray());

        Assert.True(SecretValueDetector.IsSuspicious(value));
    }

    [Fact]
    public void RGPAR05HighEntropyValueShorterThanTwentyCharactersIsNotSuspiciousByEntropyAlone()
    {
        var value = new string(Enumerable.Range(33, 19).Select(character => (char)character).ToArray());

        Assert.False(SecretValueDetector.IsSuspicious(value));
    }

    [Fact]
    public void RGPAR05OrdinaryShortLiteralIsNotSuspicious()
    {
        Assert.False(SecretValueDetector.IsSuspicious("feature-toggle"));
    }

    [Fact]
    public void RGPAR05SecretLikeNameIsDetectedWithoutInspectingTheValue()
    {
        Assert.True(SecretValueDetector.HasSecretLikeName("Payments__ApiKey"));
        Assert.False(SecretValueDetector.HasSecretLikeName("Application__Port"));
    }
}
