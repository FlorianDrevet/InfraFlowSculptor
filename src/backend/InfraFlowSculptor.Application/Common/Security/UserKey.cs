namespace InfraFlowSculptor.Application.Common.Security;

public readonly record struct UserKey(Guid TenantId, Guid ObjectId);
