using System.Reflection;
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
    {
        var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        if (type == typeof(Guid))
        {
            return Guid.NewGuid();
        }

        if (type == typeof(string))
        {
            return parameter.Name is "Code" ? "shop" : "value";
        }

        if (type == typeof(int))
        {
            return 2;
        }

        if (type == typeof(bool))
        {
            return true;
        }

        if (type == typeof(IReadOnlyList<string>))
        {
            return new[] { "alice@contoso.example" };
        }

        if (type == typeof(IReadOnlyDictionary<string, string>))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["team"] = "platform" };
        }

        throw new InvalidOperationException($"A sample value for {parameter.ParameterType.Name} is required.");
    }
}
