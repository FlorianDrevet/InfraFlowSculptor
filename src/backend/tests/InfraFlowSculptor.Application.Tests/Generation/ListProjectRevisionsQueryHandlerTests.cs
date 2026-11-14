using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Tests.Generation;

public sealed class ListProjectRevisionsQueryHandlerTests
{
    [Fact]
    public void MissingQueueEntryReturnsNoPositionInsteadOfThrowing()
    {
        var queuePositionById = new Dictionary<RevisionId, int>();

        var position = ListProjectRevisionsQueryHandler.GetQueuePosition(new RevisionId(Guid.NewGuid()), queuePositionById);

        Assert.Null(position);
    }

    [Fact]
    public void ExistingQueueEntryReturnsItsOrganizationWidePosition()
    {
        var revisionId = new RevisionId(Guid.NewGuid());
        var queuePositionById = new Dictionary<RevisionId, int> { [revisionId] = 3 };

        var position = ListProjectRevisionsQueryHandler.GetQueuePosition(revisionId, queuePositionById);

        Assert.Equal(3, position);
    }
}
