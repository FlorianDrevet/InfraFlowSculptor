using System.Diagnostics.CodeAnalysis;

namespace InfraFlowSculptor.Application.Common.Jobs;

[SuppressMessage("Naming", "CA1711", Justification = "The S-08 public contract names this enum JobQueue.")]
public enum JobQueue
{
    Generation,
    Publication,
    Tracking,
    Notifications
}
