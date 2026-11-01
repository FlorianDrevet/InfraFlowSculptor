namespace InfraFlowSculptor.Emitters.InstallKit;

internal static class ReferenceInstallResources
{
    private const string ResourcePrefix = "InfraFlowSculptor.Emitters.InstallKit.Resources.";

    public static string Read(string name)
    {
        using var stream = typeof(ReferenceInstallResources).Assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException($"The embedded install resource '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
