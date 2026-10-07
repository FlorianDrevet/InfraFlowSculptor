using System.Text.Json;

namespace InfraFlowSculptor.Application.Common.Jobs;

public static class JobSerialization
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
