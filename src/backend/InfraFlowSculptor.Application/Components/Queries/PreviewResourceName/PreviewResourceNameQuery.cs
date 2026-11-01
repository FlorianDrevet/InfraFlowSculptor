using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Engine.Naming;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.PreviewResourceName;

[RequiresPermission(Permission.ProjectRead)]
public sealed record PreviewResourceNameQuery(
    Guid ProjectId,
    Guid ComponentId,
    Guid ResourceGroupId,
    string Type,
    string LogicalName,
    Guid? ResourceId = null) : IQuery<ErrorOr<NamingPreviewResult>>, IProjectScopedRequest;
