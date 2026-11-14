using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.GitConnections;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
public sealed record SetGitConnectionRepositoryAccessCommand(
    Guid OrganizationId,
    Guid ConnectionId,
    int ExpectedVersion,
    string ProviderRepositoryId,
    bool OpenToAllProjects,
    IReadOnlyCollection<Guid> ProjectIds) : ICommand<ErrorOr<GitConnectionSummary>>;
