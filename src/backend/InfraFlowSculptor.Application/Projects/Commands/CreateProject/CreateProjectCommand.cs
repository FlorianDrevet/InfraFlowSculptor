using ErrorOr;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Application.Projects.Commands.CreateProject;

[ModelCommand("CreateProject")]
[ModelChange]
[RequiresOrganizationRole(OrganizationRole.Administrator, OrganizationRole.Member)]
public sealed record CreateProjectCommand(
    Guid ProjectId,
    Guid TenantId,
    string Name,
    string Code,
    string Description,
    string CatalogVersion,
    Guid? CoordinatorUserProfileId,
    IReadOnlyList<string> DefaultRunners,
    IReadOnlyList<string> DefaultReviewers) : IModelCommand
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
