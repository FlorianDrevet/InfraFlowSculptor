using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace InfraFlowSculptor.Architecture.Tests;

internal static class ProductionSources
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "tests",
        "obj",
        "bin",
        "Migrations"
    };

    internal static string BackendRoot { get; } = FindBackendRoot();

    internal static IReadOnlyList<string> Files { get; } = Directory
        .EnumerateFiles(BackendRoot, "*.cs", SearchOption.AllDirectories)
        .Where(IsProductionFile)
        .Order(StringComparer.Ordinal)
        .ToArray();

    internal static IReadOnlyList<SyntaxTree> SyntaxTrees { get; } = Files
        .Select(Parse)
        .ToArray();

    internal static ImmutableArray<MetadataReference> MetadataReferences { get; } = CreateMetadataReferences();

    internal static SyntaxTree Parse(string file)
    {
        return CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
    }

    internal static CSharpCompilation CreateCompilation()
    {
        return CSharpCompilation.Create(
            "InfraFlowSculptor.ProductionSources",
            SyntaxTrees,
            MetadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static bool IsProductionFile(string file)
    {
        var relativePath = Path.GetRelativePath(BackendRoot, file);
        var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return !pathParts.Any(ExcludedDirectories.Contains);
    }

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "InfraFlowSculptor.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/backend/InfraFlowSculptor.slnx from the test output directory.");
    }

    private static ImmutableArray<MetadataReference> CreateMetadataReferences()
    {
        var runtimeAssemblies = Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => assembly.Location);

        return runtimeAssemblies
            .Concat(loadedAssemblies)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToImmutableArray<MetadataReference>();
    }
}
