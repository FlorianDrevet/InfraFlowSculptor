namespace InfraFlowSculptor.Worker.Jobs;

internal static class PublicationManifestPaths
{
    private const string ManifestPath = ".ifs/manifest.json";

    public static bool IsManifestFilePath(string? filePath)
    {
        if (filePath is null)
        {
            return false;
        }

        var normalized = Normalize(filePath);
        return string.Equals(normalized, ManifestPath, StringComparison.Ordinal)
            || normalized.EndsWith(string.Concat("/", ManifestPath), StringComparison.Ordinal);
    }

    // Git paths are case-sensitive; only separator direction and a leading slash are normalized.
    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}
