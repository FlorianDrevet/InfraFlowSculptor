using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace InfraFlowSculptor.Architecture.Tests;

public sealed class OneTopLevelTypePerFileTests
{
    [Fact]
    public void ProductionFilesDeclareAtMostOnePublicTopLevelType()
    {
        var violations = new List<string>();

        foreach (var file in ProductionSources.Files)
        {
            var root = ProductionSources.Parse(file).GetCompilationUnitRoot();
            var publicTypes = root.DescendantNodes()
                .Where(IsTypeDeclaration)
                .Where(type => !type.Ancestors().Any(IsTypeDeclaration))
                .Where(IsPublic)
                .Select(GetTypeName)
                .ToArray();

            if (publicTypes.Length > 1)
            {
                violations.Add($"{Path.GetRelativePath(ProductionSources.BackendRoot, file)}: {string.Join(", ", publicTypes)}");
            }
        }

        Assert.Empty(violations);
    }

    private static bool IsTypeDeclaration(SyntaxNode node)
    {
        return node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax;
    }

    private static bool IsPublic(SyntaxNode node)
    {
        return node switch
        {
            BaseTypeDeclarationSyntax declaration => declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)),
            DelegateDeclarationSyntax declaration => declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)),
            _ => false
        };
    }

    private static string GetTypeName(SyntaxNode node)
    {
        return node switch
        {
            BaseTypeDeclarationSyntax declaration => declaration.Identifier.ValueText,
            DelegateDeclarationSyntax declaration => declaration.Identifier.ValueText,
            _ => node.Kind().ToString()
        };
    }
}
