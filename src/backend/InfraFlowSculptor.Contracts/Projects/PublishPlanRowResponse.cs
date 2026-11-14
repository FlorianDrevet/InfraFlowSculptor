namespace InfraFlowSculptor.Contracts.Projects;

public sealed record PublishPlanRowResponse(
    Guid ComponentId,
    string ComponentName,
    string ComponentCode,
    string Part,
    Guid? DestinationId,
    string DestinationName,
    string BasePath,
    string EffectiveFolder);
