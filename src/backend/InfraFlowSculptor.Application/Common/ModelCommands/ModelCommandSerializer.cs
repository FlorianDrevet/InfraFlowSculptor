using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using InfraFlowSculptor.Application.Common.Extensibility;

namespace InfraFlowSculptor.Application.Common.ModelCommands;

public sealed class ModelCommandSerializer
{
    private const string Discriminator = "$command";
    private readonly Dictionary<string, Type> typesByName;
    private readonly Dictionary<Type, string> namesByType;
    private readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ModelCommandSerializer()
    {
        var commands = typeof(IModelCommand).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                && typeof(IModelCommand).IsAssignableFrom(type))
            .Select(type => (Type: type, Attribute: type.GetCustomAttribute<ModelCommandAttribute>()))
            .Where(item => item.Attribute is not null)
            .ToArray();
        typesByName = commands.ToDictionary(item => item.Attribute!.Name, item => item.Type, StringComparer.Ordinal);
        namesByType = commands.ToDictionary(item => item.Type, item => item.Attribute!.Name);
    }

    public string Serialize(IModelCommand command)
    {
        if (!namesByType.TryGetValue(command.GetType(), out var name))
        {
            throw new InvalidOperationException($"No model command name is registered for {command.GetType().Name}.");
        }

        var payload = JsonSerializer.SerializeToNode(command, command.GetType(), options)?.AsObject()
            ?? throw new InvalidOperationException("A model command must serialize as a JSON object.");
        payload.Insert(0, Discriminator, JsonValue.Create(name));
        return payload.ToJsonString(options);
    }

    public IModelCommand Deserialize(string json)
    {
        var payload = JsonNode.Parse(json)?.AsObject()
            ?? throw new JsonException("A model command must be a JSON object.");
        var name = payload[Discriminator]?.GetValue<string>()
            ?? throw new JsonException("A model command discriminator is required.");
        if (!typesByName.TryGetValue(name, out var type))
        {
            throw new JsonException($"The model command '{name}' is not supported.");
        }

        payload.Remove(Discriminator);
        return (IModelCommand?)payload.Deserialize(type, options)
            ?? throw new JsonException($"The model command '{name}' could not be deserialized.");
    }
}
