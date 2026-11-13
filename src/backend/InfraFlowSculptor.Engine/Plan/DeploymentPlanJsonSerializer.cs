using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfraFlowSculptor.Engine.Plan;

public static class DeploymentPlanJsonSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(DeploymentPlan plan) =>
        Encoding.UTF8.GetString(SerializeToUtf8Bytes(plan));

    public static byte[] SerializeToUtf8Bytes(DeploymentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(plan, Options);
    }

    public static DeploymentPlan Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<DeploymentPlan>(json, Options)
            ?? throw new InvalidDataException("The deployment plan JSON is empty.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
