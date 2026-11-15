using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Publication;

public sealed record PublishLayoutIssue(
    string Code,
    string Message,
    Guid? ComponentId = null,
    PublishPart? Part = null,
    Guid? DestinationId = null);
