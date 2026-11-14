using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.GitConnections;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
public sealed record UpdateGitConnectionCommand(
    Guid OrganizationId,
    Guid ConnectionId,
    int ExpectedVersion,
    string Name,
    string Organization,
    Guid? TenantId,
    DateTimeOffset? PersonalAccessTokenExpiresAt,
    string? PersonalAccessToken) : ICommand<ErrorOr<GitConnectionSummary>>;
