using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Generation;

[RequiresPermission(Permission.Generate)]
public sealed record GenerateRevisionCommand(Guid ProjectId)
    : ICommand<ErrorOr<RevisionGenerationResult>>, IProjectScopedRequest;
