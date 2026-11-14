using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Publications;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetPublicationQuery(Guid ProjectId, Guid PublicationId)
    : IQuery<ErrorOr<PublicationView>>, IProjectScopedRequest;
