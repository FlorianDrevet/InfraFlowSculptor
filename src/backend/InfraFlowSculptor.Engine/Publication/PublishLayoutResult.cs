namespace InfraFlowSculptor.Engine.Publication;

public sealed record PublishLayoutResult(
    IReadOnlyCollection<PublishLayoutFile> Files,
    IReadOnlyCollection<PublishLayoutIssue> Issues);
