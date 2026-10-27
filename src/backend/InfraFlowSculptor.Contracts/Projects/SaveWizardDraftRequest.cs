using System.Text.Json;

namespace InfraFlowSculptor.Contracts.Projects;

public sealed record SaveWizardDraftRequest(JsonElement Content);
