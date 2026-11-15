using InfraFlowSculptor.Worker.Jobs;

namespace InfraFlowSculptor.Infrastructure.Tests.Publications;

public sealed class PublicationManifestTrustTests
{
    [Fact]
    public void MatchingManifestFingerprintFromPriorPublicationIsTrusted()
    {
        var manifestSha = new string('a', 64);

        Assert.True(PublicationManifestTrust.IsTrusted(manifestSha, manifestSha));
    }

    [Fact]
    public void ManifestWithoutPriorFingerprintRequiresConfirmation()
    {
        Assert.False(PublicationManifestTrust.IsTrusted(new string('a', 64), null));
    }

    [Fact]
    public void ChangedManifestFingerprintRequiresConfirmation()
    {
        Assert.False(PublicationManifestTrust.IsTrusted(new string('a', 64), new string('b', 64)));
    }
}
