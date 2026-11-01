using ErrorOr;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Domain.Tests.Links;

public sealed class LinkAggregateTests
{
    private static readonly string[] LogAnalyticsReaderRoles = ["Log Analytics Reader"];

    [Fact]
    public void CreateExplicitAccessLinkStoresNormalizedRelationshipAndRoles()
    {
        var projectId = new ProjectId(Guid.NewGuid());
        var organizationId = new OrganizationId(Guid.NewGuid());
        var sourceComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var identityResourceId = Guid.NewGuid();

        var result = LinkAggregate.Create(
            Guid.NewGuid(),
            projectId,
            organizationId,
            sourceComponentId,
            sourceResourceId,
            targetComponentId,
            targetResourceId,
            LinkKind.Access,
            LinkIdentityKind.UserAssigned,
            identityResourceId,
            [" Log Analytics Reader "],
            LinkAccessScope.Resource,
            null);

        Assert.False(result.IsError);
        Assert.Equal(projectId, result.Value.ProjectId);
        Assert.Equal(sourceResourceId, result.Value.SourceResourceId);
        Assert.Equal(targetResourceId, result.Value.TargetResourceId);
        Assert.Equal(LinkOrigin.Explicit, result.Value.Origin);
        Assert.Equal(identityResourceId, result.Value.IdentityResourceId);
        Assert.Equal(LogAnalyticsReaderRoles, result.Value.Roles.Select(role => role.Name));
    }

    [Fact]
    public void CreateWithDuplicateRolesReturnsValidationError()
    {
        var result = LinkAggregate.Create(
            Guid.NewGuid(),
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            LinkKind.Access,
            LinkIdentityKind.System,
            null,
            ["Log Analytics Reader", " log analytics reader "],
            LinkAccessScope.Resource,
            null);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "LINK_ROLE_DUPLICATE");
    }

    [Fact]
    public void CreateWithDataAccessLevelOnHostingLinkReturnsValidationError()
    {
        var result = LinkAggregate.Create(
            Guid.NewGuid(),
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            LinkKind.Hosting,
            LinkIdentityKind.None,
            null,
            [],
            LinkAccessScope.Resource,
            DataAccessLevel.Schema);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "LINK_DATA_ACCESS_LEVEL_UNSUPPORTED");
    }

    [Fact]
    public void CreateResourceTargetsItselfReturnsRgLia02()
    {
        var resourceId = Guid.NewGuid();

        var result = CreateAccessLink(resourceId, resourceId);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "RG-LIA-02");
    }

    [Fact]
    public void CreateWithEmptyEndpointReturnsFieldValidationError()
    {
        var result = LinkAggregate.Create(
            Guid.NewGuid(),
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            Guid.NewGuid(),
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            LinkKind.Access,
            LinkIdentityKind.System,
            null,
            ["Log Analytics Reader"],
            LinkAccessScope.Resource,
            null);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "LINK_FIELDS_REQUIRED");
    }

    [Fact]
    public void CreateUserAssignedIdentityWithoutResourceIdReturnsError()
    {
        var result = LinkAggregate.Create(
            Guid.NewGuid(),
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            LinkKind.Access,
            LinkIdentityKind.UserAssigned,
            null,
            ["Log Analytics Reader"],
            LinkAccessScope.Resource,
            null);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "RG-LIA-08-IDENTITY-REQUIRED");
    }

    [Fact]
    public void CreateWithUnsupportedLinkKindReturnsValidationError()
    {
        var result = LinkAggregate.Create(
            Guid.NewGuid(),
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            (LinkKind)999,
            LinkIdentityKind.None,
            null,
            [],
            LinkAccessScope.Resource,
            null);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "LINK_PARAMETER_INVALID");
    }

    private static ErrorOr<LinkAggregate> CreateAccessLink(Guid sourceResourceId, Guid targetResourceId) =>
        LinkAggregate.Create(
            Guid.NewGuid(),
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            Guid.NewGuid(),
            sourceResourceId,
            Guid.NewGuid(),
            targetResourceId,
            LinkKind.Access,
            LinkIdentityKind.System,
            null,
            ["Log Analytics Reader"],
            LinkAccessScope.Resource,
            null);
}
