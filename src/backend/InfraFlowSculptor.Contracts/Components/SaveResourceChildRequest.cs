using System.Text.Json;

namespace InfraFlowSculptor.Contracts.Components;

public sealed record SaveResourceChildRequest(string Type, string Name, JsonElement Properties, int ExpectedVersion);
