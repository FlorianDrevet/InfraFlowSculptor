namespace InfraFlowSculptor.Application.Projects.Publication;

public sealed record PublishPlanRowView(
    Guid ComponentId,
    string ComponentName,
    string ComponentCode,
    string Part,
    Guid? DestinationId,
    string DestinationName,
    string BasePath,
    string EffectiveFolder);
