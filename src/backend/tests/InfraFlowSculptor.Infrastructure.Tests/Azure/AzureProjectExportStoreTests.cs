using InfraFlowSculptor.Infrastructure.Azure;

namespace InfraFlowSculptor.Infrastructure.Tests.Azure;

public sealed class AzureProjectExportStoreTests
{
    [Fact]
    public void BlobNameUsesAnIrreversibleTokenDigestInsteadOfTheBearerToken()
    {
        const string token = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        var blobName = AzureProjectExportStore.GetBlobNameForToken(token);

        Assert.StartsWith("exports/", blobName, StringComparison.Ordinal);
        Assert.EndsWith(".json", blobName, StringComparison.Ordinal);
        Assert.DoesNotContain(token, blobName, StringComparison.Ordinal);
        Assert.Equal(blobName, AzureProjectExportStore.GetBlobNameForToken(token));
    }
}
