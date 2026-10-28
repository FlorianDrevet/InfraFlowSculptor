using System.Text.Json;

namespace InfraFlowSculptor.Contracts.Projects;

public sealed record ProjectWizardDraftResponse(JsonElement Content, DateTimeOffset ExpiresAt);
