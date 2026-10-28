using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.ModelCommands;

public sealed record ModelWorkspaceId(string Kind, Guid Id)
{
    public const string MainKind = "main";

    public static ModelWorkspaceId Main(ProjectId projectId) => new(MainKind, projectId.Value);
}
