using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Generation;

[RequiresPermission(Permission.ProjectRead)]
public sealed record ListProjectRevisionsQuery(Guid ProjectId)
    : IQuery<ErrorOr<RevisionListView>>, IProjectScopedRequest;
