using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Persistence;

public sealed class RevisionInputEntity
{
    public RevisionId RevisionId { get; set; }

    public int ModelVersion { get; set; }

    public string CatalogVersion { get; set; } = string.Empty;

    public string InfrastructureLanguage { get; set; } = string.Empty;

    public string CiPlatform { get; set; } = string.Empty;

    public string Snapshot { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;
}
