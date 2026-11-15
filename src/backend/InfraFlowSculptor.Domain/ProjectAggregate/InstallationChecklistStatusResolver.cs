namespace InfraFlowSculptor.Domain.ProjectAggregate;

public static class InstallationChecklistStatusResolver
{
    public static readonly TimeSpan VerificationFreshness = TimeSpan.FromDays(30);

    public static InstallationChecklistItemState Resolve(
        InstallationChecklistItemState generatedState,
        bool isCompleted,
        DateTimeOffset? verifiedAt,
        DateTimeOffset now)
    {
        if (generatedState == InstallationChecklistItemState.Automatic)
        {
            return InstallationChecklistItemState.Automatic;
        }

        if (verifiedAt is { } date && date <= now)
        {
            return now - date > VerificationFreshness
                ? InstallationChecklistItemState.NeedsReverification
                : InstallationChecklistItemState.Verified;
        }

        return isCompleted ? InstallationChecklistItemState.Done : InstallationChecklistItemState.ToDo;
    }
}
