using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Domain.UserProfileAggregate;

public sealed class UserProjectFavorite
{
    public UserProfileId UserProfileId { get; private set; }

    public ProjectId ProjectId { get; private set; }

    private UserProjectFavorite()
    {
    }

    internal UserProjectFavorite(UserProfileId userProfileId, ProjectId projectId)
    {
        UserProfileId = userProfileId;
        ProjectId = projectId;
    }
}
