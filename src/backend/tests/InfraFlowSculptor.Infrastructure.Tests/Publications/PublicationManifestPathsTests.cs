using InfraFlowSculptor.Worker.Jobs;

namespace InfraFlowSculptor.Infrastructure.Tests.Publications;

public sealed class PublicationManifestPathsTests
{
    [Theory]
    [InlineData(".ifs/manifest.json")]
    [InlineData("orders/.ifs/manifest.json")]
    [InlineData("orders\\.ifs\\manifest.json")]
    [InlineData("/orders/.ifs/manifest.json")]
    public void IdentifiesTheManifestFile(string? filePath)
    {
        Assert.True(PublicationManifestPaths.IsManifestFilePath(filePath));
    }

    [Fact]
    public void DoesNotIdentifyOtherPathsAsTheManifest()
    {
        Assert.False(PublicationManifestPaths.IsManifestFilePath("orders/.ifs/manifest.json.backup"));
    }

    [Fact]
    public void DoesNotIdentifyNullOrCaseDifferentPathsAsTheManifest()
    {
        Assert.False(PublicationManifestPaths.IsManifestFilePath(null));
        Assert.False(PublicationManifestPaths.IsManifestFilePath("orders/.IFS/manifest.json"));
    }
}
