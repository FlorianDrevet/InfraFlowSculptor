namespace InfraFlowSculptor.Application.Generation;

public static class ProjectExportPolicy
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);
}
