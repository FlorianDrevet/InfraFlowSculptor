namespace InfraFlowSculptor.Domain.ComponentAggregate;

public static class ApplicationSettingNameRules
{
    public const string AzureClientId = "AZURE_CLIENT_ID";

    public static bool ConflictsWithImplicit(string explicitName, string implicitName)
    {
        var normalizedExplicitName = explicitName.Trim();
        var normalizedImplicitName = implicitName.Trim();
        return normalizedImplicitName.EndsWith("__", StringComparison.Ordinal)
            ? normalizedExplicitName.StartsWith(normalizedImplicitName, StringComparison.OrdinalIgnoreCase)
            : string.Equals(normalizedExplicitName, normalizedImplicitName, StringComparison.OrdinalIgnoreCase);
    }
}
