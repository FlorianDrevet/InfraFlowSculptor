using System.Text.Json;

namespace InfraFlowSculptor.Engine.Plan;

public sealed record ResourcePropertyPlan(string Name, JsonElement? Value);
