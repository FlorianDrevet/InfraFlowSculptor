using System.Text.Json;

namespace InfraFlowSculptor.Contracts.Components;

public sealed record SetResourceOverrideRequest(Guid TargetId, string Property, JsonElement Value, int ExpectedVersion);
