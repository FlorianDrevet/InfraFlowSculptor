using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InfraFlowSculptor.Catalog;

public static class CatalogLoader
{
    private const string ResourcePrefix = "IFS-Catalog/";

    public static ICatalogProvider Load(Assembly? assembly = null)
    {
        var sourceAssembly = assembly ?? typeof(CatalogLoader).Assembly;
        var files = ReadEmbeddedFiles(sourceAssembly);
        var metadataFiles = files
            .Where(file => file.Path.StartsWith("catalog/", StringComparison.Ordinal)
                && file.Path.EndsWith("/catalog.json", StringComparison.Ordinal))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();
        var versions = ImmutableArray.CreateBuilder<CatalogVersion>(metadataFiles.Length);

        foreach (var metadataFile in metadataFiles)
        {
            using var metadataDocument = JsonDocument.Parse(metadataFile.Content);
            var metadata = metadataDocument.RootElement;
            var version = metadata.GetProperty("version").GetString()!;
            var versionPrefix = $"catalog/{version}/";
            var descriptors = files
                .Where(file => file.Path.StartsWith(versionPrefix, StringComparison.Ordinal)
                    && file.Path.StartsWith($"{versionPrefix}types/", StringComparison.Ordinal)
                    && file.Path.EndsWith(".json", StringComparison.Ordinal))
                .OrderBy(file => file.Path, StringComparer.Ordinal)
                .Select(ParseResourceType)
                .ToImmutableArray();

            var regionFile = files.Single(file => string.Equals(file.Path, $"{versionPrefix}regions.json", StringComparison.Ordinal));
            using var regionDocument = JsonDocument.Parse(regionFile.Content);
            var regions = regionDocument.RootElement.GetProperty("regions")
                .EnumerateArray()
                .Select(region => new CatalogRegion(
                    region.GetProperty("name").GetString()!,
                    region.GetProperty("code").GetString()!))
                .ToImmutableArray();

            var fingerprint = ComputeFingerprint(files.Where(file => file.Path.StartsWith(versionPrefix, StringComparison.Ordinal))
                .Select(file => new CatalogFingerprintInput(file.Path[versionPrefix.Length..], file.Content)));
            var supportEndsAtElement = metadata.GetProperty("supportEndsAt");
            DateOnly? supportEndsAt = supportEndsAtElement.ValueKind == JsonValueKind.Null
                ? null
                : DateOnly.Parse(supportEndsAtElement.GetString()!, System.Globalization.CultureInfo.InvariantCulture);

            versions.Add(new CatalogVersion(
                version,
                metadata.GetProperty("status").GetString()!,
                DateOnly.Parse(metadata.GetProperty("publishedAt").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                supportEndsAt,
                metadata.GetProperty("notes").GetString()!,
                DateOnly.Parse(metadata.GetProperty("verifiedAgainstDocsOn").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                descriptors,
                regions,
                fingerprint));
        }

        return new EmbeddedCatalogProvider(versions.ToImmutable());
    }

    private static ImmutableArray<EmbeddedJsonFile> ReadEmbeddedFiles(Assembly assembly)
    {
        var files = ImmutableArray.CreateBuilder<EmbeddedJsonFile>();
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            var normalizedName = resourceName.Replace('\\', '/');
            if (!normalizedName.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                || !normalizedName.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded catalog resource could not be opened: {resourceName}");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            files.Add(new EmbeddedJsonFile(normalizedName[ResourcePrefix.Length..], buffer.ToArray()));
        }

        return files.ToImmutable();
    }

    private static CatalogResourceTypeDescriptor ParseResourceType(EmbeddedJsonFile file)
    {
        using var document = JsonDocument.Parse(file.Content);
        return new CatalogResourceTypeDescriptor(document.RootElement);
    }

    private static string ComputeFingerprint(IEnumerable<CatalogFingerprintInput> sourceFiles)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in sourceFiles.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file.RelativePath));
            hash.AppendData([0]);
            hash.AppendData(CanonicalizeJson(file.Content));
            hash.AppendData([10]);
        }

        return $"sha256:{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}";
    }

    private static byte[] CanonicalizeJson(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output))
        {
            WriteCanonicalElement(document.RootElement, writer);
        }

        return output.WrittenSpan.ToArray();
    }

    private static void WriteCanonicalElement(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalElement(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalElement(item, writer);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON token in catalog: {element.ValueKind}");
        }
    }

    private sealed record EmbeddedJsonFile(string Path, byte[] Content);

    private sealed record CatalogFingerprintInput(string RelativePath, byte[] Content);

    private sealed class EmbeddedCatalogProvider(ImmutableArray<CatalogVersion> versions) : ICatalogProvider
    {
        public ImmutableArray<CatalogVersion> GetVersions() => versions;

        public CatalogVersion? GetVersion(string version) =>
            versions.FirstOrDefault(candidate => string.Equals(candidate.Version, version, StringComparison.OrdinalIgnoreCase));

        public CatalogVersion? LatestPublished => versions
            .Where(candidate => string.Equals(candidate.Status, "published", StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.PublishedAt)
            .ThenByDescending(candidate => GetPatchRevision(candidate.Version))
            .FirstOrDefault();

        private static int GetPatchRevision(string version)
        {
            var separator = version.LastIndexOf('.');
            return separator >= 0
                && int.TryParse(version.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var patch)
                    ? patch
                    : 0;
        }
    }
}
