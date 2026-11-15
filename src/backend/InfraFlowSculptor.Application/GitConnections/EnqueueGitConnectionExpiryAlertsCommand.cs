using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.GitConnections;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
public sealed record EnqueueGitConnectionExpiryAlertsCommand(Guid OrganizationId) : ICommand<ErrorOr<Guid>>;
