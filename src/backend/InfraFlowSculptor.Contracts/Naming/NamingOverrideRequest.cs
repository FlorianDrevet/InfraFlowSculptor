namespace InfraFlowSculptor.Contracts.Naming;

public sealed record NamingOverrideRequest(string Type, string? Template, string? Abbreviation);
