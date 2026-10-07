using System.Reflection;

namespace InfraFlowSculptor.Api.Configuration;

internal static class OpenApiBuildContext
{
    private const string DocumentGeneratorEntryAssemblyName = "GetDocument.Insider";

    public static bool IsDocumentGeneration =>
        Assembly.GetEntryAssembly()?.GetName().Name == DocumentGeneratorEntryAssemblyName;
}
