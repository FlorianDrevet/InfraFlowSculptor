using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.RevisionAggregate;
using Revision = InfraFlowSculptor.Domain.RevisionAggregate.RevisionAggregate;
using Xunit;

namespace InfraFlowSculptor.Domain.Tests.Revisions;

public sealed class RevisionAggregateTests
{
    [Fact]
    public void GeneratedRevisionKeepsItsFileTreeAndCannotChangeStateAgain()
    {
        var revision = CreateRevision();
        revision.MarkGenerating(DateTimeOffset.UtcNow);
        var files = new List<RevisionFile>
        {
            new("orders/infra/main.bicep", new string('b', 64), 12)
        };

        revision.MarkGenerated("{}", [], files, [], DateTimeOffset.UtcNow);
        files.Clear();

        Assert.Equal(RevisionState.Generated, revision.State);
        Assert.Single(revision.Files);
        Assert.Throws<InvalidOperationException>(() => revision.MarkFailed("OUTPUT_CHECK_FAILED", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void GeneratedRevisionRejectsPathsThatEscapeItsOutputTree()
    {
        var revision = CreateRevision();
        revision.MarkGenerating(DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => revision.MarkGenerated(
            "{}",
            [],
            [new RevisionFile("../outside.txt", new string('b', 64), 12)],
            [],
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void FailedRevisionCannotBeMarkedAsPublished()
    {
        var revision = CreateRevision();
        revision.MarkFailed("OUTPUT_CHECK_FAILED", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => revision.MarkPublished(DateTimeOffset.UtcNow));
    }

    private static Revision CreateRevision() => Revision.Queue(
        new RevisionId(Guid.NewGuid()),
        new OrganizationId(Guid.NewGuid()),
        new ProjectId(Guid.NewGuid()),
        1,
        new UserKey(Guid.NewGuid(), Guid.NewGuid()),
        DateTimeOffset.UtcNow,
        1,
        "2026.10-pilot.6",
        "Bicep",
        "AzureDevOps",
        new string('a', 64));
}
