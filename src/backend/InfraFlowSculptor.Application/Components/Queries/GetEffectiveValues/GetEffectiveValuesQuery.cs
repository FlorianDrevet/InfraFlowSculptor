using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetEffectiveValues;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetEffectiveValuesQuery(Guid ProjectId, Guid ResourceId)
    : IQuery<ErrorOr<IReadOnlyList<EffectiveValueTargetSummary>>>, IProjectScopedRequest;
