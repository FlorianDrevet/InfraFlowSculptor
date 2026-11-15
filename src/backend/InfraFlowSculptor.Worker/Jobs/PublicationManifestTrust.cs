namespace InfraFlowSculptor.Worker.Jobs;

internal static class PublicationManifestTrust
{
    public static bool IsTrusted(string? currentManifestSha, string? priorManifestSha) =>
        currentManifestSha is not null
        && priorManifestSha is not null
        && string.Equals(currentManifestSha, priorManifestSha, StringComparison.OrdinalIgnoreCase);
}
