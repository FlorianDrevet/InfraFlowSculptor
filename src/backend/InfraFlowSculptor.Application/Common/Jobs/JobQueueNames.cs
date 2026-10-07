namespace InfraFlowSculptor.Application.Common.Jobs;

public static class JobQueueNames
{
    public const string Generation = "generation";
    public const string Publication = "publication";
    public const string Tracking = "tracking";
    public const string Notifications = "notifications";

    public static IReadOnlyList<string> All { get; } =
    [
        Generation,
        Publication,
        Tracking,
        Notifications
    ];

    public static string GetName(JobQueue queue) => queue switch
    {
        JobQueue.Generation => Generation,
        JobQueue.Publication => Publication,
        JobQueue.Tracking => Tracking,
        JobQueue.Notifications => Notifications,
        _ => throw new ArgumentOutOfRangeException(nameof(queue), queue, "Unknown job queue.")
    };
}
