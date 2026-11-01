using ErrorOr;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Domain.Tests.Projects;

public sealed class FindingAcknowledgmentTests
{
    [Fact]
    public void CreateRequiresAComment()
    {
        var result = Create(comment: " ");

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "FINDING_ACK_COMMENT_REQUIRED");
    }

    [Fact]
    public void CreateRequiresPositiveObjectVersion()
    {
        var result = Create(objectVersion: 0);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "FINDING_ACK_VERSION_INVALID");
    }

    [Fact]
    public void CreateTrimsAndStoresAcknowledgmentContext()
    {
        var result = Create(comment: "  Vérifié dans la liste de contrôle  ", detailKey: "  Hosting  ");

        Assert.False(result.IsError);
        Assert.Equal("Vérifié dans la liste de contrôle", result.Value.Comment);
        Assert.Equal("VAL-LIA-SQL-ADMIN", result.Value.FindingCode);
        Assert.Equal("Hosting", result.Value.DetailKey);
        Assert.Equal(ObjectType.Resource, result.Value.SubjectType);
        Assert.Equal(3, result.Value.ObjectVersion);
    }

    private static ErrorOr<FindingAcknowledgmentAggregate> Create(
        string comment = "Vérifié",
        int objectVersion = 3,
        string? detailKey = null) =>
        FindingAcknowledgmentAggregate.Create(
            Guid.NewGuid(),
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            "VAL-LIA-SQL-ADMIN",
            detailKey,
            ObjectType.Resource,
            Guid.NewGuid(),
            null,
            objectVersion,
            comment,
            new UserProfileId(Guid.NewGuid()),
            DateTimeOffset.UtcNow);
}
