using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.GitConnections;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
public sealed record TestGitConnectionCommand(Guid OrganizationId, Guid ConnectionId)
    : ICommand<ErrorOr<GitConnectionSummary>>;
