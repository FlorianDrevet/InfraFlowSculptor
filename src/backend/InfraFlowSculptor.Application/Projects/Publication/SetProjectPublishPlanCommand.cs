using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Publication;

[RequiresPermission(Permission.PublicationManage)]
[Audited(AuditActions.ProjectPublicationPlanUpdated, ObjectType.Project)]
public sealed record SetProjectPublishPlanCommand(
    Guid ProjectId,
    PublishPreset Preset,
    IReadOnlyList<PublishPlanDestinationInput> Destinations,
    IReadOnlyList<PublishPlanAssignmentInput> Assignments,
    Guid? DefaultInfrastructureDestinationId,
    Guid? DefaultApplicationsDestinationId,
    Guid? DefaultSourceCodeDestinationId,
    int ExpectedVersion) : ICommand<ErrorOr<ModelCommandOutcome>>, IProjectScopedRequest;
