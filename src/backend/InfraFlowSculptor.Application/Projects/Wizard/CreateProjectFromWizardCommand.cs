using ErrorOr;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Application.Projects.Wizard;

[ModelCommand("CreateProjectFromWizard")]
[ModelChange]
[RequiresOrganizationRole(OrganizationRole.Administrator, OrganizationRole.Member)]
public sealed record CreateProjectFromWizardCommand(
    Guid ProjectId,
    Guid TenantId,
    string Name,
    string Code,
    string Description,
    string CatalogVersion,
    string NamingPreset,
    string DefaultNameTemplate,
    string ResourceGroupNameTemplate,
    IReadOnlyList<CreateProjectFromWizardEnvironment> Environments,
    IReadOnlyDictionary<string, string> Tags,
    string? PublishingPreset) : IModelCommand
{
    public ModelWorkspaceId Workspace => ModelWorkspaceId.Main(new(ProjectId));
}
