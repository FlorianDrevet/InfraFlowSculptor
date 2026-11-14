using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Publications;

[RequiresPermission(Permission.ProjectRead)]
public sealed record ListProjectPublicationsQuery(Guid ProjectId, int Limit = 30)
    : IQuery<ErrorOr<IReadOnlyCollection<PublicationView>>>, IProjectScopedRequest;
