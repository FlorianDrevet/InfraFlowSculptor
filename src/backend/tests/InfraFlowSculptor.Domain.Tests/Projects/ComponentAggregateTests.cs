using ErrorOr;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Component = InfraFlowSculptor.Domain.ComponentAggregate.ComponentAggregate;
using Project = InfraFlowSculptor.Domain.ProjectAggregate.ProjectAggregate;
using Xunit;

namespace InfraFlowSculptor.Domain.Tests.Projects;

public sealed class ComponentAggregateTests
{
    private static readonly string[] ProjectRunners = ["project-runner"];
    private static readonly string[] TargetApprovers = ["approver"];

    [Fact]
    public void CreateUsesThePerEnvironmentDefaults()
    {
        var (project, environments) = CreateProjectWithEnvironments();

        var result = CreateComponent(project, environments, "Core", "core");

        Assert.False(result.IsError);
        Assert.Equal(ComponentDeploymentMode.PerEnvironment, result.Value.Mode);
        Assert.True(result.Value.TargetsAllEnvironments);
        Assert.Empty(result.Value.TargetEnvironmentIds);
        Assert.Equal(RemovedResourceBehavior.Detach, result.Value.RemovedResourceBehavior);
        Assert.True(result.Value.DeletionProtectionEnabled);
        Assert.True(result.Value.TargetsEnvironment(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("1orders")]
    [InlineData("orders-")]
    [InlineData("Orders")]
    [InlineData("component-name-that-is-far-too-long")]
    public void CreateRejectsInvalidComponentCodes(string code)
    {
        var (project, environments) = CreateProjectWithEnvironments();

        var result = CreateComponent(project, environments, "Orders", code);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "COMPONENT_CODE_INVALID");
    }

    [Fact]
    public void CreateRejectsDuplicateNamesAndCodesIgnoringCase()
    {
        var (project, environments) = CreateProjectWithEnvironments();
        var existing = CreateComponent(project, environments, "Orders", "orders").Value;

        var duplicateName = Component.Create(
            new ComponentId(Guid.NewGuid()), project.Id, project.OrganizationId,
            "ORDERS", "orders-api", "", ComponentDeploymentMode.PerEnvironment,
            environments, existingComponents: [existing]);
        var duplicateCode = Component.Create(
            new ComponentId(Guid.NewGuid()), project.Id, project.OrganizationId,
            "Orders API", "orders", "", ComponentDeploymentMode.PerEnvironment,
            environments, existingComponents: [existing]);

        Assert.Contains(duplicateName.Errors, error => error.Code == "COMPONENT_NAME_DUPLICATE");
        Assert.Contains(duplicateCode.Errors, error => error.Code == "COMPONENT_CODE_DUPLICATE");
    }

    [Fact]
    public void PerEnvironmentComponentMustTargetAtLeastOneExistingEnvironment()
    {
        var (project, environments) = CreateProjectWithEnvironments();

        var noTarget = Component.Create(
            new ComponentId(Guid.NewGuid()), project.Id, project.OrganizationId,
            "Orders", "orders", "", ComponentDeploymentMode.PerEnvironment,
            environments, targetsAllEnvironments: false);
        var missingEnvironment = Component.Create(
            new ComponentId(Guid.NewGuid()), project.Id, project.OrganizationId,
            "Orders", "orders", "", ComponentDeploymentMode.PerEnvironment,
            environments, targetsAllEnvironments: false, targetEnvironmentIds: [Guid.NewGuid()]);

        Assert.Contains(noTarget.Errors, error => error.Code == "COMPONENT_TARGET_REQUIRED");
        Assert.Contains(missingEnvironment.Errors, error => error.Code == "COMPONENT_TARGET_ENVIRONMENT_NOT_FOUND");
    }

    [Fact]
    public void RemovingAnEnvironmentUpdatesExplicitTargetsAndCannotRemoveTheLastOne()
    {
        var (project, environments) = CreateProjectWithEnvironments();
        var component = Component.Create(
            new ComponentId(Guid.NewGuid()), project.Id, project.OrganizationId,
            "Orders", "orders", "", ComponentDeploymentMode.PerEnvironment,
            environments,
            targetsAllEnvironments: false,
            targetEnvironmentIds: environments.Select(environment => environment.Id)).Value;

        var firstRemoval = component.RemoveTargetEnvironment(environments.First().Id);
        var lastRemoval = component.RemoveTargetEnvironment(environments.Last().Id);

        Assert.False(firstRemoval.IsError);
        Assert.Equal(new[] { environments.Last().Id }, component.TargetEnvironmentIds);
        Assert.Contains(lastRemoval.Errors, error => error.Code == "COMPONENT_TARGET_REQUIRED");
    }

    [Fact]
    public void SingleTargetDefaultsFromTheLastEnvironmentAndRejectsEnvironmentCodeCollision()
    {
        var (project, environments) = CreateProjectWithEnvironments();
        var target = ComponentSingleTarget.Create(
            project.Code,
            null,
            Guid.NewGuid(),
            "francecentral",
            null,
            null,
            null,
            null,
            environments,
            ProjectRunners);

        Assert.False(target.IsError);
        Assert.Equal("shared", target.Value.Code);
        Assert.Equal($"ifs-{project.Code}-shared", target.Value.DeploymentConnection);
        Assert.True(target.Value.IsProtected);
        Assert.Equal(environments.Last().Approvers, target.Value.Approvers);
        Assert.Equal(ProjectRunners, target.Value.Runners);

        var collision = ComponentSingleTarget.Create(
            project.Code,
            "dev",
            Guid.NewGuid(),
            "francecentral",
            null,
            true,
            TargetApprovers,
            null,
            environments,
            null);

        Assert.True(collision.IsError);
        Assert.Contains(collision.Errors, error => error.Code == "COMPONENT_SINGLE_TARGET_ENVIRONMENT_CODE_CONFLICT");
    }

    [Fact]
    public void ResourceGroupCannotBeRemovedWhenItContainsResources()
    {
        var (project, environments) = CreateProjectWithEnvironments();
        var component = CreateComponent(project, environments, "Core", "core").Value;
        var group = component.AddResourceGroup(Guid.NewGuid(), "main").Value;

        var result = component.RemoveResourceGroup(group.Id, containsResources: true);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_GROUP_NOT_EMPTY");
        Assert.Single(component.ResourceGroups);
    }

    [Theory]
    [InlineData("1main")]
    [InlineData("Main")]
    [InlineData("main-group-name-that-is-way-too-long-for-a-logical-resource-group")]
    public void ResourceGroupRejectsNamesOutsideTheSpecifiedFormat(string name)
    {
        var (project, environments) = CreateProjectWithEnvironments();
        var component = CreateComponent(project, environments, "Core", "core").Value;

        var result = component.AddResourceGroup(Guid.NewGuid(), name);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_GROUP_NAME_INVALID");
    }

    [Fact]
    public void ResourceGroupNamesAreUniqueWithinTheComponent()
    {
        var (project, environments) = CreateProjectWithEnvironments();
        var component = CreateComponent(project, environments, "Core", "core").Value;
        Assert.False(component.AddResourceGroup(Guid.NewGuid(), "main").IsError);

        var duplicate = component.AddResourceGroup(Guid.NewGuid(), "main");

        Assert.True(duplicate.IsError);
        Assert.Contains(duplicate.Errors, error => error.Code == "RESOURCE_GROUP_NAME_DUPLICATE");
    }

    private static ErrorOr<Component> CreateComponent(
        Project project,
        IReadOnlyCollection<ProjectEnvironment> environments,
        string name,
        string code) => Component.Create(
            new ComponentId(Guid.NewGuid()),
            project.Id,
            project.OrganizationId,
            name,
            code,
            "",
            ComponentDeploymentMode.PerEnvironment,
            environments);

    private static (Project Project, IReadOnlyCollection<ProjectEnvironment> Environments)
        CreateProjectWithEnvironments()
    {
        var project = Project.Create(
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            "Shop",
            "shop",
            new UserProfileId(Guid.NewGuid())).Value;
        project.AddEnvironment(
            Guid.NewGuid(), "Development", "dev", Guid.NewGuid(), "francecentral", 1,
            false, [], [], "");
        project.AddEnvironment(
            Guid.NewGuid(), "Production", "prd", Guid.NewGuid(), "francecentral", 2,
            true, ["approver@contoso.example"], [], "");
        return (project, project.Environments.ToArray());
    }
}
