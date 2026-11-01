namespace InfraFlowSculptor.Application.Generation;

public readonly record struct EmitterKey(IacLanguage Language, PulumiLanguage? PulumiLanguage = null);
