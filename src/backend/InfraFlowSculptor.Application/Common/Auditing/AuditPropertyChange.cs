using System.Text.Json;

namespace InfraFlowSculptor.Application.Common.Auditing;

public sealed record AuditPropertyChange(
    string Name,
    JsonElement? Before,
    JsonElement? After,
    string? PersonalDataField);
