using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Application.Generation;

public static class RevisionInputJsonSerializer
{
    public const string Schema = "ifs-project/v1";

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static byte[] SerializeToUtf8Bytes(ModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var normalizedSnapshot = snapshot with
        {
            ObjectVersions = new Dictionary<ObjectRef, int>(snapshot.ObjectVersions)
        };
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(
            new RevisionInputDocument(Schema, normalizedSnapshot),
            Options);
    }

    public static string Serialize(ModelSnapshot snapshot) =>
        Encoding.UTF8.GetString(SerializeToUtf8Bytes(snapshot));

    public static ModelSnapshot Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<RevisionInputDocument>(json, Options)
            ?? throw new InvalidDataException("The frozen project input is empty.");
        if (!string.Equals(document.Schema, Schema, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The frozen project input schema is unsupported.");
        }

        return document.Snapshot;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new ObjectVersionsConverter());
        return options;
    }

    private sealed class ObjectVersionsConverter : JsonConverter<IReadOnlyDictionary<ObjectRef, int>>
    {
        public override IReadOnlyDictionary<ObjectRef, int> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("Object versions must be an array.");
            }

            var result = new Dictionary<ObjectRef, int>();
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                var type = Enum.Parse<ObjectType>(entry.GetProperty("type").GetString()!, ignoreCase: true);
                var id = entry.GetProperty("id").GetGuid();
                var version = entry.GetProperty("version").GetInt32();
                result.Add(new ObjectRef(type, id), version);
            }

            return result;
        }

        public override void Write(
            Utf8JsonWriter writer,
            IReadOnlyDictionary<ObjectRef, int> value,
            JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var entry in value.OrderBy(entry => entry.Key.Type).ThenBy(entry => entry.Key.Id))
            {
                writer.WriteStartObject();
                writer.WriteString("type", entry.Key.Type.ToString());
                writer.WriteString("id", entry.Key.Id);
                writer.WriteNumber("version", entry.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }
    }
}
