using ErrorOr;
using System.Text.Json;
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

    [Fact]
    public void ComponentStoresOptionalDefaultAndTypeSpecificNaming()
    {
        var (project, environments) = CreateProjectWithEnvironments();
        var component = CreateComponent(project, environments, "Core", "core").Value;

        var defaultNaming = InvokeRequired(component, "ConfigureDefaultNameTemplate", "{component}-{name}-{env}");
        var typeNaming = InvokeRequired(component, "ConfigureTypeNamingOverride", "KeyVault", "kv-{component}-{name}-{env}", "vault");

        Assert.False(ReadProperty<bool>(defaultNaming, "IsError"));
        Assert.False(ReadProperty<bool>(typeNaming, "IsError"));
        Assert.Equal("{component}-{name}-{env}", ReadProperty<string>(component, "DefaultNameTemplate"));
        var naming = Assert.Single((System.Collections.IEnumerable)ReadProperty<object>(component, "NamingOverrides"));
        Assert.Equal("KeyVault", ReadProperty<string>(naming!, "Type"));
        Assert.Equal("kv-{component}-{name}-{env}", ReadProperty<string>(naming!, "Template"));
        Assert.Equal("vault", ReadProperty<string>(naming!, "Abbreviation"));
    }

    [Fact]
    public void PublishedComponentCannotChangeNamingTemplatesOrOverrides()
    {
        var (project, environments) = CreateProjectWithEnvironments();
        var component = CreateComponent(project, environments, "Core", "core").Value;
        component.RecordPublication(DateTimeOffset.UtcNow);

        var defaultTemplate = component.ConfigureDefaultNameTemplate("{component}-{name}-{env}");
        var typeOverride = component.ConfigureTypeNamingOverride("KeyVault", "kv-{component}-{name}-{env}", "vault");

        Assert.Contains(defaultTemplate.Errors, error => error.Code == "PROJECT_NAMING_LOCKED_AFTER_PUBLICATION");
        Assert.Contains(typeOverride.Errors, error => error.Code == "PROJECT_NAMING_LOCKED_AFTER_PUBLICATION");
    }

    [Fact]
    public void PublishedComponentCannotChangeForcedResourceName()
    {
        var (project, environments) = CreateProjectWithEnvironments();
        var component = CreateComponent(project, environments, "Core", "core").Value;
        var group = component.AddResourceGroup(Guid.NewGuid(), "main").Value;
        var resource = component.AddResource(
            Guid.NewGuid(), group.Id, "KeyVault", "main", "", false,
            environments.Select(environment => environment.Id), new Dictionary<string, JsonElement>()).Value;
        component.RecordPublication(DateTimeOffset.UtcNow);

        var result = component.SetResourceForcedName(resource.Id, environments.First().Id, "kv-shop-core-main-dev");

        Assert.Contains(result.Errors, error => error.Code == "PROJECT_NAMING_LOCKED_AFTER_PUBLICATION");
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
    public void SingleComponentResourceUsesItsStableTargetIdInsteadOfAnEnvironmentId()
    {
        var (project, environments) = CreateProjectWithEnvironments();
        var singleTarget = ComponentSingleTarget.Create(
            project.Code,
            "shared",
            Guid.NewGuid(),
            "francecentral",
            null,
            true,
            TargetApprovers,
            null,
            environments,
            ProjectRunners).Value;
        var component = Component.Create(
            new ComponentId(Guid.NewGuid()),
            project.Id,
            project.OrganizationId,
            "Platform",
            "platform",
            "",
            ComponentDeploymentMode.Single,
            environments,
            targetsAllEnvironments: false,
            targetEnvironmentIds: [],
            singleTarget).Value;
        var group = component.AddResourceGroup(Guid.NewGuid(), "main").Value;
        var resource = component.AddResource(
            Guid.NewGuid(), group.Id, "KeyVault", "shared", "", true,
            [singleTarget.Id], new Dictionary<string, JsonElement>()).Value;

        Assert.False(resource.Presences.Single().IsPresent);
        Assert.False(component.SetExistingResourceId(resource.Id, singleTarget.Id, "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/shared").IsError);
        Assert.True(resource.Presences.Single().IsPresent);
        Assert.True(resource.Presences.Single().TargetId == singleTarget.Id);
        Assert.True(resource.ExistingIds.Single().TargetId == singleTarget.Id);

        var replacementTarget = ComponentSingleTarget.Create(
            project.Code, "shared", Guid.NewGuid(), "francecentral", null, true,
            TargetApprovers, null, environments, ProjectRunners).Value;
        Assert.NotEqual(singleTarget.Id, replacementTarget.Id);
        var updated = component.Update(
            component.Name,
            component.Code,
            component.Description,
            ComponentDeploymentMode.Single,
            environments,
            targetsAllEnvironments: false,
            selectedEnvironmentIds: [],
            replacementTarget,
            component.RemovedResourceBehavior,
            component.DeletionProtectionEnabled);

        Assert.False(updated.IsError);
        Assert.Equal(singleTarget.Id, component.SingleTarget!.Id);
        Assert.Contains(resource.Presences, presence => presence.TargetId == singleTarget.Id && presence.IsPresent);
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

    [Fact]
    public void ResourceValuesAreStoredAsTypedTreeNodesAndOverridesCanBeClearedExplicitly()
    {
        var (project, environments) = CreateProjectWithEnvironments();
        var component = CreateComponent(project, environments, "Core", "core").Value;
        var group = component.AddResourceGroup(Guid.NewGuid(), "main").Value;
        using var propertiesDocument = JsonDocument.Parse("""{"workloadProfiles":[{"name":"D4","minimumCount":2}],"zoneRedundant":false}""");
        var properties = propertiesDocument.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);

        var created = component.AddResource(
            Guid.NewGuid(), group.Id, "ContainerAppsEnvironment", "main", "", false,
            environments.Select(environment => environment.Id), properties);

        Assert.False(created.IsError);
        Assert.Equal(2, created.Value.Presences.Count(presence => presence.IsPresent));
        Assert.Equal(5, created.Value.Values.Count);
        Assert.Contains(created.Value.Values, node => node.Kind == ResourceValueKind.WholeNumber && node.IntegerValue == 2);
        Assert.Contains(created.Value.Values, node => node.Kind == ResourceValueKind.Sequence && node.Position is null);
        var restoredProfiles = created.Value.GetProperties()["workloadProfiles"];
        Assert.Equal("D4", restoredProfiles[0].GetProperty("name").GetString());
        Assert.Equal(2, restoredProfiles[0].GetProperty("minimumCount").GetInt32());

        using var emptyDocument = JsonDocument.Parse("\"\"");
        var empty = emptyDocument.RootElement.Clone();
        var environmentId = environments.First().Id;
        var set = component.SetResourceOverride(created.Value.Id, environmentId, "zoneRedundant", empty);

        Assert.False(set.IsError);
        var overrideRoot = Assert.Single(created.Value.Values, node => node.TargetId == environmentId && node.ParentId is null);
        Assert.Equal(ResourceValueKind.Textual, overrideRoot.Kind);
        Assert.Equal(string.Empty, overrideRoot.StringValue);

        var cleared = component.ClearResourceOverride(created.Value.Id, environmentId, "zoneRedundant");

        Assert.False(cleared.IsError);
        Assert.DoesNotContain(created.Value.Values, node => node.TargetId == environmentId);
        Assert.Contains(created.Value.Values, node => node.TargetId is null && node.PropertyName == "zoneRedundant");
    }

    [Fact]
    public void ResourceValuesRejectNestingBeyondTheRelationalTreeLimit()
    {
        var (project, environments) = CreateProjectWithEnvironments();
        var component = CreateComponent(project, environments, "Core", "core").Value;
        var group = component.AddResourceGroup(Guid.NewGuid(), "main").Value;
        using var nestedValue = JsonDocument.Parse(new string('[', 33) + "0" + new string(']', 33));
        var result = component.AddResource(
            Guid.NewGuid(), group.Id, "ContainerAppsEnvironment", "main", "", false,
            environments.Select(environment => environment.Id),
            new Dictionary<string, JsonElement> { ["workloadProfiles"] = nestedValue.RootElement.Clone() });

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "RESOURCE_PROPERTY_VALUE_INVALID");
        Assert.Empty(component.Resources);
    }

    [Fact]
    public void ExistingResourceStartsAbsentAndBecomesPresentAfterItsIdentifierIsSet()
    {
        var (project, environments) = CreateProjectWithEnvironments();
        var component = CreateComponent(project, environments, "Core", "core").Value;
        var group = component.AddResourceGroup(Guid.NewGuid(), "main").Value;
        var created = component.AddResource(
            Guid.NewGuid(), group.Id, "KeyVault", "legacy", "", true,
            environments.Select(environment => environment.Id), new Dictionary<string, JsonElement>());

        Assert.False(created.IsError);
        Assert.All(created.Value.Presences, presence => Assert.False(presence.IsPresent));

        var environmentId = environments.First().Id;
        var assigned = component.SetExistingResourceId(created.Value.Id, environmentId,
            "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/legacy");

        Assert.False(assigned.IsError);
        Assert.True(created.Value.Presences.Single(presence => presence.TargetId == environmentId).IsPresent);
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

    private static object InvokeRequired(object target, string methodName, params object?[] arguments)
    {
        var method = target.GetType().GetMethod(methodName);
        Assert.NotNull(method);
        return method!.Invoke(target, arguments)!;
    }

    private static T ReadProperty<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<T>(property!.GetValue(target));
    }

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
