using System.Text.Json;

namespace InfraFlowSculptor.Contracts.Components;

public sealed record SaveResourcePropertiesRequest(JsonElement Properties, int ExpectedVersion);
