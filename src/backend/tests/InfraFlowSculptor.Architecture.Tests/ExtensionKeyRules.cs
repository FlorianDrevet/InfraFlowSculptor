using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace InfraFlowSculptor.Architecture.Tests;

internal static class ExtensionKeyRules
{
    internal static IReadOnlyList<string> FindSwitchOrComparisonViolations(
        CSharpCompilation compilation,
        IEnumerable<Type> extensionKeyTypes)
    {
        var keySymbols = new List<INamedTypeSymbol>();

        foreach (var extensionKeyType in extensionKeyTypes)
        {
            if (!extensionKeyType.IsEnum)
            {
                throw new ArgumentException($"{extensionKeyType.FullName} is not an enum.", nameof(extensionKeyTypes));
            }

            var metadataName = extensionKeyType.FullName ?? extensionKeyType.Name;
            var keySymbol = compilation.GetTypeByMetadataName(metadataName);
            if (keySymbol is null)
            {
                throw new ArgumentException($"The compilation does not contain {metadataName}.", nameof(extensionKeyTypes));
            }

            keySymbols.Add(keySymbol);
        }

        var violations = new List<string>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var switchStatement in root.DescendantNodes().OfType<SwitchStatementSyntax>())
            {
                if (IsRegistryImplementation(switchStatement))
                {
                    continue;
                }

                if (IsExtensionKey(semanticModel.GetTypeInfo(switchStatement.Expression).Type, keySymbols))
                {
                    violations.Add(Describe(syntaxTree, switchStatement));
                }
            }

            foreach (var switchExpression in root.DescendantNodes().OfType<SwitchExpressionSyntax>())
            {
                if (IsRegistryImplementation(switchExpression))
                {
                    continue;
                }

                if (IsExtensionKey(semanticModel.GetTypeInfo(switchExpression.GoverningExpression).Type, keySymbols))
                {
                    violations.Add(Describe(syntaxTree, switchExpression));
                }
            }

            foreach (var comparison in root.DescendantNodes().OfType<BinaryExpressionSyntax>()
                         .Where(node => node.IsKind(SyntaxKind.EqualsExpression) || node.IsKind(SyntaxKind.NotEqualsExpression)))
            {
                if (IsRegistryImplementation(comparison))
                {
                    continue;
                }

                var leftType = semanticModel.GetTypeInfo(comparison.Left).Type;
                var rightType = semanticModel.GetTypeInfo(comparison.Right).Type;

                if (IsExtensionKey(leftType, keySymbols) || IsExtensionKey(rightType, keySymbols))
                {
                    violations.Add(Describe(syntaxTree, comparison));
                }
            }
        }

        return violations;
    }

    private static bool IsExtensionKey(ITypeSymbol? type, IReadOnlyCollection<INamedTypeSymbol> keySymbols)
    {
        return type is not null && keySymbols.Any(key => SymbolEqualityComparer.Default.Equals(key, type));
    }

    private static bool IsRegistryImplementation(SyntaxNode node)
    {
        var containingType = node.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        return containingType is not null
            && (containingType.Identifier.ValueText.EndsWith("Registry", StringComparison.Ordinal)
                || containingType.BaseList?.Types.Any(baseType => baseType.Type.ToString().Contains("Registry", StringComparison.Ordinal)) == true);
    }

    private static string Describe(SyntaxTree syntaxTree, SyntaxNode node)
    {
        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        return $"{Path.GetFileName(syntaxTree.FilePath)}:{line}";
    }
}
