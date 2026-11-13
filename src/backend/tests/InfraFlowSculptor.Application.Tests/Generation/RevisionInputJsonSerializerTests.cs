using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Model;
using Xunit;

namespace InfraFlowSculptor.Application.Tests.Generation;

public sealed class RevisionInputJsonSerializerTests
{
    [Fact]
    public void SerializeCanonicalizesDictionaryOrderAndRoundTripsObjectVersions()
    {
        var firstObject = new ObjectRef(ObjectType.Project, Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var secondObject = new ObjectRef(ObjectType.Resource, Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var first = CreateSnapshot(
            new Dictionary<string, string> { ["zeta"] = "last", ["alpha"] = "first" },
            new Dictionary<ObjectRef, int> { [secondObject] = 4, [firstObject] = 3 });
        var second = CreateSnapshot(
            new Dictionary<string, string> { ["alpha"] = "first", ["zeta"] = "last" },
            new Dictionary<ObjectRef, int> { [firstObject] = 3, [secondObject] = 4 });

        var serialized = RevisionInputJsonSerializer.Serialize(first);

        Assert.Equal(serialized, RevisionInputJsonSerializer.Serialize(second));
        Assert.Contains("\"schema\":\"ifs-project/v1\"", serialized, StringComparison.Ordinal);
        var restored = RevisionInputJsonSerializer.Deserialize(serialized);
        Assert.Equal(3, restored.ObjectVersions[firstObject]);
        Assert.Equal(4, restored.ObjectVersions[secondObject]);
    }

    [Fact]
    public void DeserializeRejectsUnsupportedSnapshotSchemas()
    {
        var json = RevisionInputJsonSerializer.Serialize(CreateSnapshot(
                new Dictionary<string, string>(),
                new Dictionary<ObjectRef, int>()))
            .Replace("ifs-project/v1", "ifs-project/v2", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => RevisionInputJsonSerializer.Deserialize(json));
    }

    private static ModelSnapshot CreateSnapshot(
        IReadOnlyDictionary<string, string> tags,
        IReadOnlyDictionary<ObjectRef, int> objectVersions) =>
        new(
            new ProjectSnapshot("Pilot", "pilot", "")
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000100"),
                Version = 7,
                ModelVersion = 4,
                CatalogVersion = "2026.10-pilot.6",
                InfrastructureLanguage = "Bicep",
                CiPlatform = "AzureDevOps"
            },
            [],
            new NamingConventionSnapshot("compact", "{resource}", "rg-{component}-{env}"),
            tags,
            [])
        {
            ObjectVersions = objectVersions
        };
}
