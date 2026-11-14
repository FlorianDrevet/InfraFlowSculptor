using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Generation;

[RequiresPermission(Permission.Generate)]
[Audited(AuditActions.RevisionGenerationRequested, ObjectType.Revision)]
public sealed record GenerateRevisionCommand(Guid ProjectId)
    : ICommand<ErrorOr<RevisionGenerationResult>>, IProjectScopedRequest;
