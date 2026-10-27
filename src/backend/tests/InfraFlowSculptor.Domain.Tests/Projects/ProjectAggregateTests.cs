using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Project = InfraFlowSculptor.Domain.ProjectAggregate.ProjectAggregate;
using Xunit;

namespace InfraFlowSculptor.Domain.Tests.Projects;

public sealed class ProjectAggregateTests
{
    [Fact]
    public void CreateAssignsCreatorAsNamedOwner()
    {
        var creatorId = new UserProfileId(Guid.NewGuid());
        var project = CreateProject(creatorId);

        Assert.True(project.HasRole(creatorId, ProjectRole.Owner));
        Assert.Contains(Permission.ProjectAdminister, project.EffectivePermissions(creatorId));
    }

    [Theory]
    [InlineData("1shop")]
    [InlineData("Shop")]
    [InlineData("shop-with-dashes")]
    [InlineData("abcdefghij1")]
    public void CreateRejectsProjectCodeOutsideTheJ0Format(string code)
    {
        var result = Project.Create(
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            "Shop",
            code,
            new UserProfileId(Guid.NewGuid()));

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "PROJECT_CODE_INVALID");
    }

    [Theory]
    [InlineData("Hi")]
    [InlineData("A project name that is longer than eighty characters because the project names have a fixed maximum length")]
    public void CreateRejectsProjectNameOutsideTheSpecifiedLength(string name)
    {
        var result = Project.Create(
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            name,
            "shop",
            new UserProfileId(Guid.NewGuid()));

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "PROJECT_NAME_INVALID");
    }

    [Fact]
    public void AddingEnvironmentRequiresAnApproverWhenProtected()
    {
        var project = CreateProject(new UserProfileId(Guid.NewGuid()));

        var result = project.AddEnvironment(
            Guid.NewGuid(), "Production", "prd", Guid.NewGuid(), "francecentral", 1, true, [], [], "");

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "RG-ENV-APPROVER-REQUIRED");
    }

    [Fact]
    public void InsertingEnvironmentPreservesContinuousOrder()
    {
        var project = CreateProject(new UserProfileId(Guid.NewGuid()));
        Assert.False(project.AddEnvironment(Guid.NewGuid(), "Development", "dev", Guid.NewGuid(), "francecentral", 1, false, [], [], "").IsError);
        Assert.False(project.AddEnvironment(Guid.NewGuid(), "Production", "prd", Guid.NewGuid(), "francecentral", 2, false, [], [], "").IsError);

        var result = project.AddEnvironment(Guid.NewGuid(), "Test", "test", Guid.NewGuid(), "francecentral", 2, false, [], [], "");

        Assert.False(result.IsError);
        Assert.Equal(3, project.Environments.Count);
        Assert.Equal(1, project.Environments.Single(environment => environment.Code == "dev").Position);
        Assert.Equal(2, project.Environments.Single(environment => environment.Code == "test").Position);
        Assert.Equal("prd", project.Environments.Single(environment => environment.Code == "prd").Code);
        Assert.Equal(3, project.Environments.Single(environment => environment.Code == "prd").Position);
    }

    [Fact]
    public void ProjectCodeCannotChangeAfterFirstPublication()
    {
        var project = CreateProject(new UserProfileId(Guid.NewGuid()));
        project.RecordPublication(DateTimeOffset.UtcNow);

        var result = project.ChangeCode("shop2");

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "RG-PRJ-01-CODE-LOCKED");
    }

    [Fact]
    public void EnvironmentCodeCannotChangeAfterFirstPublication()
    {
        var project = CreateProject(new UserProfileId(Guid.NewGuid()));
        var environment = project.AddEnvironment(
            Guid.NewGuid(), "Production", "prd", Guid.NewGuid(), "francecentral", 1, false, [], [], "").Value;
        project.RecordPublication(DateTimeOffset.UtcNow);

        var result = project.UpdateEnvironment(
            environment.Id,
            environment.Name,
            "prod",
            environment.SubscriptionId,
            environment.Region,
            environment.IsProtected,
            environment.Approvers,
            environment.Runners,
            environment.Description,
            [],
            confirmImpact: true);

        Assert.Contains(result.Errors, error => error.Code == "RG-ENV-03-CODE-LOCKED");
    }

    [Fact]
    public void EnvironmentUpdateCanChangeItsDeploymentConnection()
    {
        var project = CreateProject(new UserProfileId(Guid.NewGuid()));
        var environment = project.AddEnvironment(
            Guid.NewGuid(), "Development", "dev", Guid.NewGuid(), "francecentral", 1, false, [], [], "").Value;

        var result = project.UpdateEnvironment(
            environment.Id,
            environment.Name,
            environment.Code,
            environment.SubscriptionId,
            environment.Region,
            environment.IsProtected,
            environment.Approvers,
            environment.Runners,
            environment.Description,
            [],
            confirmImpact: false,
            deploymentConnection: "shared-dev-connection");

        Assert.False(result.IsError);
        Assert.Equal("shared-dev-connection", environment.DeploymentConnection);
    }

    [Fact]
    public void UserCannotDeclareSystemTags()
    {
        var project = CreateProject(new UserProfileId(Guid.NewGuid()));

        var result = project.SetTags([new KeyValuePair<string, string>("Managed-By", "someone")]);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "RG-PRJ-06-SYSTEM-TAG-RESERVED");
    }

    [Fact]
    public void ProjectRejectsMoreThanFiftyDeclaredTags()
    {
        var project = CreateProject(new UserProfileId(Guid.NewGuid()));
        var tags = Enumerable.Range(1, 51)
            .Select(index => new KeyValuePair<string, string>($"tag-{index}", "value"));

        var result = project.SetTags(tags);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "RG-PRJ-07-TAG-LIMIT");
    }

    [Fact]
    public void EnvironmentNamesAndCodesAreUniqueWithoutRegardToCase()
    {
        var project = CreateProject(new UserProfileId(Guid.NewGuid()));
        Assert.False(project.AddEnvironment(Guid.NewGuid(), "Development", "dev", Guid.NewGuid(), "francecentral", 1, false, [], [], "").IsError);

        var duplicateName = project.AddEnvironment(
            Guid.NewGuid(), "DEVELOPMENT", "test", Guid.NewGuid(), "francecentral", 2, false, [], [], "");
        var duplicateCode = project.AddEnvironment(
            Guid.NewGuid(), "Test", "DEV", Guid.NewGuid(), "francecentral", 2, false, [], [], "");

        Assert.Contains(duplicateName.Errors, error => error.Code == "ENVIRONMENT_NAME_DUPLICATE");
        Assert.Contains(duplicateCode.Errors, error => error.Code == "ENVIRONMENT_CODE_DUPLICATE");
    }

    [Fact]
    public void ProjectStartsWithEmptyDefaultRunnersAndReviewers()
    {
        var project = CreateProject(new UserProfileId(Guid.NewGuid()));

        Assert.Empty(project.DefaultRunners);
        Assert.Empty(project.DefaultReviewers);
    }

    [Fact]
    public void ProjectNormalizesDefaultRunnersAndReviewers()
    {
        var project = Project.Create(
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            "Shop",
            "shop",
            Guid.NewGuid(),
            "",
            "2026.10-pilot",
            new UserProfileId(Guid.NewGuid()),
            null,
            [" linux ", "LINUX", "windows"],
            [" alice ", "ALICE"]);

        Assert.False(project.IsError);
        Assert.Equal(["linux", "windows"], project.Value.DefaultRunners);
        Assert.Equal(["alice"], project.Value.DefaultReviewers);
    }

    [Fact]
    public void PublishedEnvironmentTargetConfirmationIncludesKnownAffectedResources()
    {
        var project = CreateProject(new UserProfileId(Guid.NewGuid()));
        var environment = project.AddEnvironment(
            Guid.NewGuid(), "Production", "prd", Guid.NewGuid(), "francecentral", 1, false, [], [], "").Value;
        project.RecordPublication(DateTimeOffset.UtcNow);

        var result = project.UpdateEnvironment(
            environment.Id,
            environment.Name,
            environment.Code,
            Guid.NewGuid(),
            "westeurope",
            environment.IsProtected,
            environment.Approvers,
            environment.Runners,
            environment.Description,
            environment.Tags.Select(tag => new KeyValuePair<string, string>(tag.Key, tag.Value)),
            confirmImpact: false);

        Assert.True(result.IsError);
        var error = Assert.Single(result.Errors, candidate => candidate.Code == "RG-ENV-03-CONFIRMATION-REQUIRED");
        Assert.NotNull(error.Metadata);
        Assert.Contains("affectedResources", error.Metadata!.Keys);
    }

    [Fact]
    public void RemovingLastNamedOwnerFailsWithRgOrg11()
    {
        var ownerId = new UserProfileId(Guid.NewGuid());
        var project = CreateProject(ownerId);

        var result = project.RemoveRole(ownerId, ProjectRole.Owner);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "RG-ORG-11");
        Assert.True(project.HasRole(ownerId, ProjectRole.Owner));
    }

    [Fact]
    public void RemovingAllRolesForMemberRemovesAssignmentsAndAdvancesVersion()
    {
        var ownerId = new UserProfileId(Guid.NewGuid());
        var memberId = new UserProfileId(Guid.NewGuid());
        var project = CreateProject(ownerId);
        Assert.False(project.AssignRole(memberId, ProjectRole.Contributor).IsError);
        Assert.False(project.AssignRole(memberId, ProjectRole.Auditor).IsError);
        var previousVersion = project.Version;

        var result = project.RemoveAllRolesForMember(memberId);

        Assert.False(result.IsError);
        Assert.DoesNotContain(project.RoleAssignments, assignment => assignment.Principal == memberId);
        Assert.Equal(previousVersion + 1, project.Version);
    }

    [Fact]
    public void RemovingAllRolesForLastOwnerIsRejected()
    {
        var ownerId = new UserProfileId(Guid.NewGuid());
        var project = CreateProject(ownerId);

        var result = project.RemoveAllRolesForMember(ownerId);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "RG-ORG-11");
        Assert.True(project.HasRole(ownerId, ProjectRole.Owner));
    }

    [Fact]
    public void TakeOwnershipAddsOwnerWithoutRemovingCurrentOwnerAndIsIdempotent()
    {
        var currentOwnerId = new UserProfileId(Guid.NewGuid());
        var organizationAdministratorId = new UserProfileId(Guid.NewGuid());
        var project = CreateProject(currentOwnerId);

        Assert.False(project.TakeOwnership(organizationAdministratorId).IsError);
        Assert.False(project.TakeOwnership(organizationAdministratorId).IsError);

        Assert.True(project.HasRole(currentOwnerId, ProjectRole.Owner));
        Assert.True(project.HasRole(organizationAdministratorId, ProjectRole.Owner));
        Assert.Equal(2, project.RoleAssignments.Count(assignment => assignment.Role == ProjectRole.Owner));
    }

    [Fact]
    public void EffectivePermissionsUnionsRolesAndIncludesImplicitProjectRead()
    {
        var ownerId = new UserProfileId(Guid.NewGuid());
        var memberId = new UserProfileId(Guid.NewGuid());
        var project = CreateProject(ownerId);
        Assert.False(project.AssignRole(memberId, ProjectRole.Reader).IsError);
        Assert.False(project.AssignRole(memberId, ProjectRole.Auditor).IsError);

        var permissions = project.EffectivePermissions(memberId);

        Assert.Equal(
            new[] { Permission.ProjectRead, Permission.AuditRead }.Order(StringComparer.Ordinal),
            permissions.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void UnsupportedScopeCannotBeAssigned()
    {
        var ownerId = new UserProfileId(Guid.NewGuid());
        var project = CreateProject(ownerId);

        var result = project.AssignRole(new UserProfileId(Guid.NewGuid()), ProjectRole.Reader, (ProjectRoleScope)99);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "PROJECT_ROLE_INVALID");
    }

    private static Project CreateProject(UserProfileId ownerId) =>
        Project.Create(
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            "Shop",
            "shop",
            ownerId).Value;
}
