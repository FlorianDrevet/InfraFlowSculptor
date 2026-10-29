using System.Reflection;
using System.Text.Json;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;

namespace InfraFlowSculptor.Application.Tests.Common.ModelCommands;

public sealed class ModelCommandSerializerTests
{
    [Fact]
    public void EveryRegisteredModelCommandRoundTripsByItsStableName()
    {
        var serializer = new ModelCommandSerializer();
        var commandTypes = typeof(IModelCommand).Assembly.GetTypes()
            .Where(type => !type.IsAbstract
                && !type.IsInterface
                && typeof(IModelCommand).IsAssignableFrom(type)
                && type.GetCustomAttribute<ModelCommandAttribute>() is not null)
            .ToArray();

        Assert.NotEmpty(commandTypes);
        foreach (var commandType in commandTypes)
        {
            var constructor = Assert.Single(commandType.GetConstructors());
            var arguments = constructor.GetParameters().Select(CreateValue).ToArray();
            var command = Assert.IsAssignableFrom<IModelCommand>(Activator.CreateInstance(commandType, arguments));

            var serialized = serializer.Serialize(command);
            var restored = serializer.Deserialize(serialized);

            Assert.Equal(commandType, restored.GetType());
            Assert.Equal(serialized, serializer.Serialize(restored));
        }
    }

    [Fact]
    public void UnknownCommandNameIsRejected()
    {
        var serializer = new ModelCommandSerializer();

        var exception = Assert.Throws<System.Text.Json.JsonException>(
            () => serializer.Deserialize("{\"$command\":\"Unknown\"}"));

        Assert.Contains("Unknown", exception.Message, StringComparison.Ordinal);
    }

    private static object? CreateValue(ParameterInfo parameter)
        => CreateValue(parameter.ParameterType, parameter.Name);

    private static object? CreateValue(Type parameterType, string? parameterName)
    {
        var type = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
        if (type == typeof(Guid))
        {
            return Guid.NewGuid();
        }

        if (type == typeof(JsonElement))
        {
            using var document = JsonDocument.Parse("{}");
            return document.RootElement.Clone();
        }

        if (type == typeof(string))
        {
            return parameterName is "Code" ? "shop" : "value";
        }

        if (type == typeof(int))
        {
            return 2;
        }

        if (type == typeof(bool))
        {
            return true;
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0)!;
        }

        if (type.IsGenericType
            && (type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                || type.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)
                || type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
        {
            var itemType = type.GetGenericArguments()[0];
            var values = Array.CreateInstance(itemType, 1);
            values.SetValue(
                itemType == typeof(string) ? "alice@contoso.example" : CreateValue(itemType, null),
                0);
            return values;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
        {
            var arguments = type.GetGenericArguments();
            var dictionaryType = typeof(Dictionary<,>).MakeGenericType(arguments);
            var dictionary = Activator.CreateInstance(dictionaryType)!;
            dictionaryType.GetMethod("Add")!.Invoke(dictionary,
            [
                CreateValue(arguments[0], "Key"),
                CreateValue(arguments[1], "Value")
            ]);
            return dictionary;
        }

        var constructor = type.GetConstructors().SingleOrDefault();
        if (constructor is not null)
        {
            var arguments = constructor.GetParameters().Select(CreateValue).ToArray();
            return Activator.CreateInstance(type, arguments);
        }

        throw new InvalidOperationException($"A sample value for {parameterType.Name} is required.");
    }
}
