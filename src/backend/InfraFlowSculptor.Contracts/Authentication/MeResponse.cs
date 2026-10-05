namespace InfraFlowSculptor.Contracts.Authentication;

public sealed record MeResponse(Guid TenantId, Guid ObjectId, string DisplayName, string? VerifiedEmail);
