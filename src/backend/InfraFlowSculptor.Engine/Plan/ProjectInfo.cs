using System.Text.Json.Serialization;

namespace InfraFlowSculptor.Engine.Plan;

public sealed record ProjectInfo(
    string Name,
    string Code,
    string Description,
    string InfrastructureLanguage,
    string CiPlatform,
    string ModuleSource,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Repositories = null);
