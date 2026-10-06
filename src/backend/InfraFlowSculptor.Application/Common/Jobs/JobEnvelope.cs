using System.Text.Json;
using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Jobs;

public sealed record JobEnvelope(
    Guid JobId,
    OrganizationId OrganizationId,
    string Type,
    JsonElement Payload,
    string? TraceParent);
