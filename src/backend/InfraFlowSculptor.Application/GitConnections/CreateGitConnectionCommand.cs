using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.GitConnections;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
public sealed record CreateGitConnectionCommand(
    Guid OrganizationId,
    Guid ConnectionId,
    GitProviderKind ProviderKind,
    string Name,
    string Organization,
    Guid? TenantId,
    GitConnectionAuthenticationMode AuthenticationMode,
    DateTimeOffset? PersonalAccessTokenExpiresAt,
    string? PersonalAccessToken) : ICommand<ErrorOr<GitConnectionSummary>>;
