namespace InfraFlowSculptor.Catalog;

public sealed record CatalogPropertyValueFailure(
    CatalogPropertyValueFailureKind Kind,
    string? MemberName = null)
{
    public string DisplayMemberName => CatalogValueDisplay.SafeDisplay(MemberName ?? string.Empty);
}
