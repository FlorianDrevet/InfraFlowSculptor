using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Domain.RevisionAggregate;

namespace InfraFlowSculptor.Application.Tests.Generation;

public sealed class RevisionChangeFactoryTests
{
    [Fact]
    public void CreateReportsAddedChangedAndRemovedFilesInPathOrder()
    {
        RevisionFile[] previous =
        [
            new("infra/keep.bicep", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 1),
            new("infra/change.bicep", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", 1),
            new("infra/remove.bicep", "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc", 1)
        ];
        RevisionFile[] current =
        [
            new("infra/add.bicep", "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd", 1),
            new("infra/change.bicep", "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", 2),
            new("infra/keep.bicep", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 1)
        ];

        var changes = RevisionChangeFactory.Create(previous, current);

        Assert.Collection(
            changes,
            change => Assert.Equal(("Added", "infra/add.bicep"), (change.Category, change.Subject)),
            change => Assert.Equal(("Changed", "infra/change.bicep"), (change.Category, change.Subject)),
            change => Assert.Equal(("Removed", "infra/remove.bicep"), (change.Category, change.Subject)));
    }

    [Fact]
    public void CreateMarksEveryFileAddedForFirstRevision()
    {
        RevisionFile[] current =
        [
            new("infra/main.bicep", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 1)
        ];

        var change = Assert.Single(RevisionChangeFactory.Create(null, current));

        Assert.Equal("Added", change.Category);
        Assert.Equal("infra/main.bicep", change.Subject);
    }
}
