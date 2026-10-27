using System.Collections.Immutable;

namespace InfraFlowSculptor.Catalog;

public interface ICatalogProvider
{
    ImmutableArray<CatalogVersion> GetVersions();

    CatalogVersion? GetVersion(string version);

    CatalogVersion? LatestPublished { get; }
}
